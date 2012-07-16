using System;
using System.Collections.Generic;
using System.Linq;
using Kayak;
using Kayak.Http;

namespace NancyKayakDemo
{
    using ResultAction = Action<
            string,
            IDictionary<string, string>,
            Func< //body
                Func< //next
                    ArraySegment<byte>, // data
                    Action, // continuation
                    bool>, // continuation was or will be invoked
                Action<Exception>, //error
                Action, //complete
                Action>>; //cancel

    using AppAction = Action< // app
        IDictionary<string, object>, // env
        Action< // result
            string, // status
            IDictionary<string, string>, // headers
            Func< // body
                Func< // next
                    ArraySegment<byte>, // data
                    Action, // continuation
                    bool>, // async
                Action<Exception>, // error
                Action, // complete
                Action>>, // cancel
        Action<Exception>>; // error

    using BodyAction = Func< //body
        Func< //next
            ArraySegment<byte>, // data
            Action, // continuation
            bool>, // continuation was or will be invoked
        Action<Exception>, //error
        Action, //complete
        Action>; // cancel

    internal class OwinHttpRequestDelegate : IHttpRequestDelegate
    {
        private readonly AppAction owin;

        internal OwinHttpRequestDelegate(AppAction owin)
        {
            this.owin = owin;
        }

        private static IDictionary<string, object> ToOwinEnvironment(HttpRequestHead head)
        {
            var env = new Dictionary<string, object>
                          {
                              {"owin.RequestMethod", head.Method ?? ""},
                              {"owin.RequestPath", head.Path},
                              {"owin.RequestPathBase", ""},
                              {"owin.RequestQueryString", head.QueryString ?? ""},
                              {"owin.RequestScheme", "http"},
                              {"owin.RequestHeaders", head.Headers ?? new Dictionary<string, string>()},
                              {"owin.Version", "1.0"}
                          };

            return env;
        }

        public void OnRequest(
            HttpRequestHead head,
            IDataProducer body,
            IHttpResponseDelegate response)
        {
            var env = ToOwinEnvironment(head);

            if (body == null)
                env["owin.RequestBody"] = null;
            else
            {
                BodyAction bodyFunc = (onData, onError, onEnd) =>
                {
                    var d = body.Connect(new DataConsumer(onData, onError, onEnd));
                    return () => { if (d != null) d.Dispose(); };
                };

                env["owin.RequestBody"] = bodyFunc;
            }

            owin(env, HandleResponse(response), HandleError(response));
        }

        ResultAction HandleResponse(IHttpResponseDelegate response)
        {
            return (status, headers, body) =>
            {
                if (headers == null)
                    headers = new Dictionary<string, string>();

                if (body != null &&
                    !headers.Keys.Contains("content-length", StringComparer.OrdinalIgnoreCase) &&
                    !headers.Keys.Contains("transfer-encoding", StringComparer.OrdinalIgnoreCase))
                {
                    // consume body and calculate Content-Length
                    BufferBody(response)(status, headers, body);
                }
                else
                {
                    response.OnResponse(new HttpResponseHead
                                            {
                        Status = status,
                        Headers = headers.ToDictionary(kv => kv.Key, kv => string.Join("\r\n", kv.Value.ToArray()), StringComparer.OrdinalIgnoreCase),
                    }, body == null ? null : new DataProducer(body));
                }
            };
        }

        ResultAction BufferBody(IHttpResponseDelegate response)
        {
            return (status, headers, body) =>
            {
                var buffer = new LinkedList<ArraySegment<byte>>();

                body((data, continuation) =>
                {
                    var copy = new byte[data.Count];
                    Buffer.BlockCopy(data.Array, data.Offset, copy, 0, data.Count);
                    buffer.AddLast(new ArraySegment<byte>(copy));
                    return false;
                },
                HandleError(response),
                () =>
                {
                    var contentLength = buffer.Aggregate(0, (r, i) => r + i.Count);

                    IDataProducer responseBody = null;

                    if (contentLength > 0)
                    {
                        headers["Content-Length"] = contentLength.ToString();

                        responseBody = new DataProducer((onData, onError, onComplete) =>
                        {
                            bool cancelled = false;

                            while (!cancelled && buffer.Count > 0)
                            {
                                var next = buffer.First;
                                buffer.RemoveFirst();
                                onData(next.Value, null);
                            }

                            onComplete();

                            buffer = null;

                            return () => cancelled = true;
                        });
                    }

                    response.OnResponse(new HttpResponseHead
                                            {
                        Status = status,
                        Headers = headers
                    }, responseBody);
                });
            };
        }

        Action<Exception> HandleError(IHttpResponseDelegate response)
        {
            return error =>
            {
                Console.Error.WriteLine("Error from Owin application.");
                Console.Error.WriteStackTrace(error);

                response.OnResponse(new HttpResponseHead
                                        {
                    Status = "503 Internal Server Error",
                    Headers = new Dictionary<string, string>
                                  {
                        { "Connection", "close" }
                    }
                }, null);
            };
        }
    }

    internal class DataConsumer : IDataConsumer
    {
        readonly Func<ArraySegment<byte>, Action, bool> onData;
        readonly Action<Exception> onError;
        readonly Action onEnd;

        public DataConsumer(
            Func<ArraySegment<byte>, Action, bool> onData,
            Action<Exception> onError,
            Action onEnd)
        {
            this.onData = onData;
            this.onError = onError;
            this.onEnd = onEnd;
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            return onData(data, continuation);
        }

        public void OnEnd()
        {
            onEnd();
        }

        public void OnError(Exception e)
        {
            onError(e);
        }
    }

    internal class DataProducer : IDataProducer
    {
        readonly BodyAction body;

        public DataProducer(BodyAction body)
        {
            this.body = body;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            return new Disposable(body(
                (data, continuation) => channel.OnData(data, continuation),
                error => channel.OnError(error),
                () => channel.OnEnd()));
        }
    }

    internal class Disposable : IDisposable
    {
        readonly Action dispose;

        public Disposable(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            dispose();
        }
    }
}
