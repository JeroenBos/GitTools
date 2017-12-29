using JBSnorro;
using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CI.UI
{
    public sealed class CIReceivingPipe : ReceivingPipe
    {
        public new const string PipeName = "CI_Pipe";
        public const string PipeMessageSeparator = "-:-";

        public static CIReceivingPipe Start(Program program, CancellationToken cancellationToken = default(CancellationToken))
        {
            CIReceivingPipe ctor(string arg0, string arg1) => new CIReceivingPipe(arg0, arg1, program);

            return Start<CIReceivingPipe>(PipeName, PipeMessageSeparator, ctor: ctor, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the dispatcher that created this pipe. 
        /// </summary>
        public Dispatcher MainDispatcher { get; }
        /// <summary>
        /// Gets the program that is currently executing.
        /// </summary>
        public Program Program { get; }
        /// <summary>
        /// Gets the cancellation token for this pipe.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        private CIReceivingPipe(string arg0, string arg1, Program program)
            : base(arg0, arg1)
        {
            Contract.Requires(program != null);

            this.Program = program;
            this.MainDispatcher = Dispatcher.CurrentDispatcher;
        }

        protected override void HandleMessage(string[] message, CancellationToken cancellationToken)
        {
            try
            {
                Program.HandleInput(message, cancellationToken);
            }
            catch (Exception e)
            {
                MainDispatcher.InvokeAsync(() => Program.OutputError(e));
            }
        }
    }
}
