using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace RMLViewer3D
{
    // no COM/Threading considerations...
    public class SimplePrintModule
    {
        private static PrintQueueCollection AllPrinters { get; set; }
        private static PrintQueue SelectedPrinter { get; set; }

        public SimplePrintModule()
        {
            AllPrinters = GetAllPrinters();
            if (AllPrinters.Any())
            {
                SelectedPrinter = GuessRolandPrinter() ?? AllPrinters.First();
                SelectedPrinter.Refresh();
            }
        }

        private static PrintQueueCollection GetAllPrinters()
        {
            var server = new LocalPrintServer();
            return server.GetPrintQueues();
        }

        private static PrintQueue GuessRolandPrinter()
        {
            var keywords = new[]{"Roland", "Modela", "MDX"};
            var printers = GetAllPrinters();
            return printers.FirstOrDefault(pq => keywords.Any(k => 
                pq.FullName.ToLower().Contains(k.ToLower())));
        }

        public void PrintLines(params string[] lines)
        {
            if(SelectedPrinter != null)
            {
                //var c = SelectedPrinter.GetPrintCapabilities();
                
                // check here to fix exception:
                //https://msdn.microsoft.com/en-us/library/ms552914(v=vs.110).aspx
                // or, look into com port-based approach
                var myPrintJob = SelectedPrinter.AddJob("Test");

                // Write a Byte buffer to the JobStream and close the stream
                using(var myStream = myPrintJob.JobStream)
                {
                    var myByteBuffer = Encoding.Unicode.GetBytes(string.Join("\n", lines));
                    myStream.Write(myByteBuffer, 0, myByteBuffer.Length);
                }
            }
        }
    }
}
