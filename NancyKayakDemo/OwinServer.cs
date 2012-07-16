using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Kayak;
using Kayak.Http;

namespace NancyKayakDemo
{
    // Give the owin app Action a shorthand:
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

    public class OwinServer
    {
        private readonly IScheduler scheduler;
        private readonly IServer server;

        public OwinServer(AppAction owin)
            : this(owin, null, null)
        {
        }

        public OwinServer(
            AppAction owin,
            Action<IScheduler, Exception> onException,
            Action<IScheduler> onStop)
        {
            scheduler = KayakScheduler.Factory.Create(new DefaultSchedulerDelegate(onException, onStop));

            // The OwinServer uses an internal implementation of IHttpRequestDelegate:
            server = KayakServer.Factory.CreateHttp(
                new OwinHttpRequestDelegate(owin),
                scheduler);
        }

        public void Start(IPAddress address, int port)
        {
            var endPoint = new IPEndPoint(address, port);

            Task task = new Task(() =>
                                     {
                                        using (server.Listen(endPoint))
                                            scheduler.Start();
                                     });
            task.Start();
        }

        public void Stop()
        {
            scheduler.Stop();
        }

        class DefaultSchedulerDelegate : ISchedulerDelegate
        {
            private readonly Action<IScheduler, Exception> onException;
            private readonly Action<IScheduler> onStop;

            public DefaultSchedulerDelegate(
                Action<IScheduler, Exception> onException,
                Action<IScheduler> onStop)
            {
                this.onException = onException;
                this.onStop = onStop;
            }

            public void OnException(IScheduler scheduler, Exception e)
            {
                if (onException != null)
                    onException(scheduler, e);
            }

            public void OnStop(IScheduler scheduler)
            {
                if (onStop != null)
                    onStop(scheduler);
            }
        }
    }
}
