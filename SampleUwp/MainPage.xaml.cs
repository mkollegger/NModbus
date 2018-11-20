using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using NModbus;
using NModbus.IO;
using NModbus.Serial;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SampleUwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private IModbusSerialMaster _master;

        public MainPage()
        {
            this.InitializeComponent();
        }




        private async Task<IStreamResource> GetModBusUwP(string usbName = "USB-RS485 Cable")
        {
            SerialDeviceAdapter result = null;

            var id = (await SerialPortHelper.GetSerialDevices()).FirstOrDefault(s => s.Name == usbName);
            if (id == null)
            {
                Debug.WriteLine($"ModBusAdapter: {usbName} not found!");
                return null;
            }

            var device = await SerialPortHelper.GetSerialDevice(id.UsbId);
            if (device == null)
            {
                Debug.WriteLine($"ModBusAdapter: {usbName} not found!");
                return null;
            }

            device.BaudRate = 9600;
            device.DataBits = 8;
            device.Parity = SerialParity.None;
            device.StopBits = SerialStopBitCount.One;
            device.Handshake = SerialHandshake.None;
            device.ErrorReceived += (sender, args) =>
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debug.WriteLine($"RS485 I/O ERROR: {args}");
                }
            };

            result = new SerialDeviceAdapter(device);
            return result;
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (_master == null)
            {
                var port = await GetModBusUwP();
                if (port != null)
                {
                    var modbusFactory = new ModbusFactory();
                    _master = modbusFactory.CreateRtuMaster(port);
                    _master.Transport.ReadTimeout = 1000;
                    _master.Transport.WriteTimeout = 1000;
                    _master.Transport.Retries = 3;
                    _master.Transport.WaitToRetryMilliseconds = 250;
                }
            }

            if (_master == null)
            {
                InfoTxt.Text = "No ModBus!";
                return;
            }

            var r1 = await _master.ReadInputRegistersAsync(1, 35061, 2);

            try
            {
                var r = await _master.ReadInputRegistersAsync(9, 35061, 2);
                UInt32 value = (UInt32) r[0] << 16 | r[1];
                Debug.WriteLine($"WeatherStation Operating time: {value}s");

            }
            catch (Exception exception)
            {
                ErrorTxt.Text = "Timeout!";
            }

            for (int i = 0; i < 10000; i++)
            {
                var ra = await _master.ReadInputRegistersAsync(1, 35001, 64);
                Debug.WriteLine($"{i+1} Bytes Read: {ra.Length}");
                InfoTxt.Text = $"{i + 1} Bytes Read: {ra.Length}";
            }

        }
    }




    public class SerialPortDeviceInfos
    {
        #region Properties

        /// <summary>
        ///     Name des Gerätes (nicht der COM Port)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Id des Gerätes
        /// </summary>
        public string UsbId { get; set; }

        #endregion


        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"{Name} ({UsbId})";
        }
    }

    /// <summary>
    ///     <para>Hilfsfunktionen für die serielle Kommunikation</para>
    ///     Klasse SerialPortHelper. (C) 2018 FOTEC Forschungs- und Technologietransfer GmbH
    /// </summary>
    public static class SerialPortHelper
    {
        public static async Task<List<SerialPortDeviceInfos>> GetSerialDevices()
        {
            var aqs = SerialDevice.GetDeviceSelector();
            var deviceCollection = await DeviceInformation.FindAllAsync(aqs);
            return deviceCollection.Select(item => new SerialPortDeviceInfos { Name = item.Name, UsbId = item.Id }).ToList();
        }

        /// <summary>
        /// COM Port via USB Id erzeugen
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public static async Task<SerialDevice> GetSerialDevice(string deviceId)
        {
            SerialDevice result = null;
            try
            {
                result = await SerialDevice.FromIdAsync(deviceId);
            }
            catch
            {
                Debug.WriteLine($"GetSerialDevice: Could not find device {deviceId}");
            }

            return result;
        }

      


    }
}
