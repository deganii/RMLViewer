using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace RMLViewer3D
{
    public class SerialModule
    {
        private string[] AllPorts { get; set; }
        private string _portName;
        private SerialPort _port;

        //private static Thread _serialThread;
        //private static AutoResetEvent _are;
        
        public SerialModule()
        {
            AllPorts = SerialPort.GetPortNames();
            //_portName = AllPorts[0];
        }

        private static void InitializeSerial()
        {
 
        }

        public void Connect(string portName = null)
        {
            if (portName != null)
            {
                _portName = portName;
            }
            _port = new SerialPort(_portName, 9600, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = true
            };
            _port.DataReceived +=PortOnDataReceived;
            _port.Open();
        }

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs serialDataReceivedEventArgs)
        {
            var data = _port.ReadExisting();
            Debug.WriteLine(serialDataReceivedEventArgs.EventType);
            Debug.WriteLine(data);
        }

        public void Disconnect()
        {
            _port.DiscardOutBuffer();
            _port.DiscardInBuffer();
            _port.Close();
            _port.DataReceived -= PortOnDataReceived;
            _port = null;
        }

        public void Write(params string[] rml)
        {
            foreach (var t in rml.Select(line => line.Trim()))
            {
                while (!_port.CtsHolding || !_port.DsrHolding)
                {
                    Thread.Sleep(10);
                }
                _port.Write(t);
            }
        }
    }
}
