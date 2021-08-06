using System;
using System.Collections.Generic;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    internal class TpmClient : IDisposable
    {
        public Tpm2Device Device { get; private set; }
        public Tpm2 Tpm { get; private set; }

        private const string DefaultSimulatorName = "127.0.0.1";
        private const int DefaultSimulatorPort = 2321;

        internal static TpmClient CreateDeviceClient()
        {
            Tpm2Device tpmDevice = new TbsDevice();
            tpmDevice.Connect();

            var tpm = new Tpm2(tpmDevice);

            TpmClient client = new TpmClient(tpmDevice, tpm);
            return client;
        }

        internal static TpmClient CreateSimulatorClient()
        {
            Tpm2Device tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
            tpmDevice.Connect();

            var tpm = new Tpm2(tpmDevice);
            tpmDevice.PowerCycle();
            tpm.Startup(Su.Clear);

            TpmClient client = new TpmClient(tpmDevice, tpm);
            return client;
        }

        public void Dispose()
        {
            if (Tpm != null)
                Tpm.Dispose();

            if (Device != null)
                Device.Dispose();
        }

        private TpmClient(Tpm2Device device, Tpm2 tpm)
        {
            Device = device;
            Tpm = tpm;
        }
    }
}
