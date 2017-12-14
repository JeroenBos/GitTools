using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CI.UI
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var n = new NotificationIcon())
                Thread.Sleep(10000);
        }
    }
}
