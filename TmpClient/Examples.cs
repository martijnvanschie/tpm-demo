using System;
using System.Collections.Generic;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    internal class Examples
    {
        /// <summary>
        /// If using a TCP connection, the default DNS name/IP address for the
        /// simulator.
        /// </summary>
        private const string DefaultSimulatorName = "127.0.0.1";

        /// <summary>
        /// If using a TCP connection, the default TCP port of the simulator.
        /// </summary>
        private const int DefaultSimulatorPort = 2321;

        public static void ConnectLocal()
        {
            using (Tpm2Device tpmDevice = new TbsDevice())
            {
                tpmDevice.Connect();
            }
        }

        public static void ConnectSimulator()
        {
            using (Tpm2Device tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort))
            {
                tpmDevice.Connect();
            }
        }

        public static Tpm2Device Connect(bool useSimulator = false)
        {
            if (useSimulator)
            {
                return new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
            }
            else
            {
                return new TbsDevice();
            }
        }

        internal static void PrintAlgorithms(Tpm2Device device)
        {
            try
            {
                device.Connect();

                using (var tpm = new Tpm2(device))
                {
                    ICapabilitiesUnion caps;
                    tpm.GetCapability(Cap.Algs, 0, 1000, out caps);
                    var algsx = (AlgPropertyArray)caps;

                    Console.WriteLine("Supported algorithms:");
                    foreach (var alg in algsx.algProperties)
                    {
                        Console.WriteLine("  {0}", alg.alg.ToString());
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal static void PrintCommandss(Tpm2Device device)
        {
            try
            {
                device.Connect();

                using (var tpm = new Tpm2(device))
                {
                    ICapabilitiesUnion caps;
                    tpm.GetCapability(Cap.TpmProperties, (uint)Pt.TotalCommands, 1, out caps);
                    tpm.GetCapability(Cap.Commands, (uint)TpmCc.First, TpmCc.Last - TpmCc.First + 1, out caps);

                    var commands = (CcaArray)caps;
                    Console.WriteLine("Supported commands:");
                    List<TpmCc> implementedCc = new List<TpmCc>();
                    foreach (var attr in commands.commandAttributes)
                    {
                        var commandCode = (TpmCc)((uint)attr & 0x0000FFFFU);
                        implementedCc.Add(commandCode);
                        Console.WriteLine("  {0}", commandCode.ToString());
                    }

                    Console.WriteLine("Commands from spec not implemented:");
                    foreach (var cc in Enum.GetValues(typeof(TpmCc)))
                    {
                        if (!implementedCc.Contains((TpmCc)cc))
                        {
                            Console.WriteLine("  {0}", cc.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal static void AvailablePCRBanks(Tpm2Device device)
        {
            try
            {
                device.Connect();

                using (var tpm = new Tpm2(device))
                {
                    //
                    // Read PCR attributes. Cap.Pcrs returns the list of PCRs which are supported
                    // in different PCR banks. The PCR banks are identified by the hash algorithm
                    // used to extend values into the PCRs of this bank.
                    // 
                    ICapabilitiesUnion caps;
                    tpm.GetCapability(Cap.Pcrs, 0, 255, out caps);
                    PcrSelection[] pcrs = ((PcrSelectionArray)caps).pcrSelections;

                    Console.WriteLine();
                    Console.WriteLine("Available PCR banks:");
                    foreach (PcrSelection pcrBank in pcrs)
                    {
                        var sb = new StringBuilder();
                        sb.AppendFormat("PCR bank for algorithm {0} has registers at index:", pcrBank.hash);
                        sb.AppendLine();
                        foreach (uint selectedPcr in pcrBank.GetSelectedPcrs())
                        {
                            sb.AppendFormat("{0},", selectedPcr);
                        }
                        Console.WriteLine(sb);
                    }

                    //
                    // Read PCR attributes. Cap.PcrProperties checks for certain properties of each PCR register.
                    // 
                    tpm.GetCapability(Cap.PcrProperties, 0, 255, out caps);

                    Console.WriteLine();
                    Console.WriteLine("PCR attributes:");
                    TaggedPcrSelect[] pcrProperties = ((TaggedPcrPropertyArray)caps).pcrProperty;
                    foreach (TaggedPcrSelect pcrProperty in pcrProperties)
                    {
                        if ((PtPcr)pcrProperty.tag == PtPcr.None)
                        {
                            continue;
                        }

                        uint pcrIndex = 0;
                        var sb = new StringBuilder();
                        sb.AppendFormat("PCR property {0} supported by these registers: ", (PtPcr)pcrProperty.tag);
                        sb.AppendLine();
                        foreach (byte pcrBitmap in pcrProperty.pcrSelect)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                if ((pcrBitmap & (1 << i)) != 0)
                                {
                                    sb.AppendFormat("{0},", pcrIndex);
                                }
                                pcrIndex++;
                            }
                        }
                        Console.WriteLine(sb);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
