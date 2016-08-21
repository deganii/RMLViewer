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
    public class PrintModule
    {
        private static PrintQueueCollection AllPrinters { get; set; }
        private static PrintQueue SelectedPrinter { get; set; }
        private static Thread _printingThread;
        private static AutoResetEvent _are;
        private static bool _shutdown;
        private static bool _printJob;
        private static string[] _lines;

        static PrintModule()
        {
            _are = new AutoResetEvent(false);
            _printingThread = new Thread(PrintThread) {IsBackground = true};
            _printingThread.SetApartmentState(ApartmentState.STA);
            _printingThread.Start();
        }

        private static void PrintThread()
        {
            AllPrinters = GetAllPrinters();
            if (AllPrinters.Any())
            {
                SelectedPrinter = GuessRolandPrinter() ?? AllPrinters.First();
                SelectedPrinter.Refresh();
            }

            // wait for print jobs
            //Dispatcher.Run();
            while(true)
            {
                _are.WaitOne();
                if(_shutdown)
                {
                    return;
                }
                if(_printJob && _lines != null)
                {
                    DoPrintLines();
                    _printJob = false;
                    _lines = null;
                    
                }
            }

        }

        public void Close()
        {
            _shutdown = true;
            _are.Set();

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

        private static void SetPrinter(PrintQueue printer)
        {
            SelectedPrinter = printer;
        }

        public static void PrintLines(params string[] lines)
        {
            _printJob = true;
            _lines = lines;
            _are.Set();
        }

        private static void DoPrintLines()
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
                    var myByteBuffer = Encoding.Unicode.GetBytes(string.Join("\n", _lines));
                    myStream.Write(myByteBuffer, 0, myByteBuffer.Length);
                }
            }
        }
    }
}
