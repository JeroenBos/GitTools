using JBSnorro;
using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CI.UI
{
    public sealed class CIReceivingPipe : ReceivingPipe
    {
        public new const string PipeName = "CI_Pipe";
        public const string PipeMessageSeparator = "-:-";

        public static async Task<CIReceivingPipe> Start()
        {
            CIReceivingPipe ctor(string arg0, string arg1, Dispatcher arg2) => new CIReceivingPipe(arg0, arg1, arg2);

            return await Start<CIReceivingPipe>(PipeName, PipeMessageSeparator, ownDispatcher: true, ctor: ctor);
        }

        /// <summary>
        /// Gets the dispatcher that created this pipe. 
        /// </summary>
        public Dispatcher MainDispatcher { get; }

        private CIReceivingPipe(string arg0, string arg1, Dispatcher arg2)
            : base(arg0, arg1, arg2)
        {
            this.MainDispatcher = Dispatcher.CurrentDispatcher;
        }

        protected override void HandleMessage(string[] message)
        {
            try
            {
                Program.HandleInput(message);
            }
            catch (Exception e)
            {
                MainDispatcher.InvokeAsync(() => Program.OutputError(e));
            }
        }
    }
}
