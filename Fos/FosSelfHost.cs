using System;
using System.Collections.Generic;
using System.Threading;
using FastCgiNet;
using Fos.Owin;
using Fos.Listener;
using Fos.Logging;
using Owin;

namespace Fos
{
    using OwinHandler = Func<IDictionary<string, object>, System.Threading.Tasks.Task>;

	/// <summary>
	/// This is the class you need to use to host your web application. It will create a Socket that receives FastCgi
	/// connections from your web server, passing them to the Owin pipeline.
	/// </summary>
	public class FosSelfHost : SocketListener
	{
		private FosAppBuilder AppBuilder;
		private OwinHandler OwinPipelineEntry;
		private Action<IAppBuilder> ApplicationConfigure;
		private CancellationTokenSource OnAppDisposal;
        private IServerLogger UserSetLogger;

        /// <summary>
        /// Set this to enable a statistics logger alongside any logger the user sets.
        /// </summary>
        internal StatsLogger StatisticsLogger;

        /// <summary>
        /// Some applications need to control when flushing the response to the visitor is done; if that is the case, set this to <c>false</c>.
        /// If the application does not require that, then Fos will flush the response itself periodically to avoid keeping too 
        /// many buffers around and to avoid an idle connection. The default value for this is <c>true</c>.
        /// </summary>
        public bool FlushPeriodically = true;

        private IServerLogger BuildLogger()
        {
            if (UserSetLogger == null && StatisticsLogger == null)
                return null;

            var logger = new CompositeServerLogger();
            if (UserSetLogger != null)
                logger.AddLogger(UserSetLogger);
            if (StatisticsLogger != null)
                logger.AddLogger(StatisticsLogger);

            return logger;
        }

        internal override void OnRecordBuilt(FosRequest req, RecordBase rec)
        {
            req.ApplicationPipelineEntry = OwinPipelineEntry;
            req.FlushPeriodically = FlushPeriodically;
        }

		/// <summary>
		/// Starts this FastCgi server! This method only returns when the server is ready to accept connections.
		/// </summary>
		/// <param name="background">True to start the server without blocking, false to block.</param>
		public override void Start(bool background)
		{
			AppBuilder = new FosAppBuilder(OnAppDisposal.Token);

            // Configure the application and build our pipeline entry. This must happen BEFORE BUILDING THE LOGGER
            ApplicationConfigure(AppBuilder);
            OwinPipelineEntry = (OwinHandler) AppBuilder.Build(typeof(OwinHandler));

            // Sets the logger
            var logger = BuildLogger();
            if (logger != null)
                base.SetLogger(logger);

            base.Start(background);
		}

		/// <summary>
		/// Notifies the application (in non-standard fashion) that the server is stopping and stops listening for connections, while
		/// closing active connections abruptly.
		/// </summary>
		public override void Stop()
		{
			// Tell the application the server is disposing
			//OnAppDisposal.Cancel();
			OnAppDisposal.Dispose();

            base.Stop();
		}

		public override void SetLogger(IServerLogger logger)
		{
            if (logger == null)
                throw new ArgumentNullException("logger");

            this.UserSetLogger = logger;
 		}

		public override void Dispose()
		{
			OnAppDisposal.Dispose();
			base.Dispose();
		}

		/// <summary>
		/// This constructor lets you specify the method to be executed to register your application's middleware.
		/// </summary>
		public FosSelfHost(Action<IAppBuilder> configureMethod)
		{
			ApplicationConfigure = configureMethod;
			OnAppDisposal = new CancellationTokenSource();
		}
	}
}
