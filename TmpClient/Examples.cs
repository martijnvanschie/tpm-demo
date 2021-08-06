using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    internal class Examples
    {
        private const UInt32 AIOTH_PERSISTED_URI_INDEX = 0x01400100;
        private const UInt32 AIOTH_PERSISTED_KEY_HANDLE = 0x81000100;
        private const UInt32 SRK_HANDLE = 0x81000001;

        private static UInt32 logicalDeviceId = 1;

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

        public static Tpm2Device GetSimulator()
        {
            Tpm2Device tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
            return tpmDevice;
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

        internal static void PrintAlgorithms(Tpm2 tpm)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal static void PrintCommandss(Tpm2 tpm)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal static void AvailablePCRBanks(Tpm2 tpm)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        //internal static GetDeviceId()
        //{
        //    for (logicalDeviceId = 0; logicalDeviceId < 10; logicalDeviceId++)
        //    {
        //        if (GetDeviceId().CompareTo(DeviceIdName) == 0)
        //        {
        //            break;
        //        }
        //    }
        //    if (logicalDeviceId > 9)
        //    {
        //        throw new IndexOutOfRangeException();
        //    }
        //}

        internal static string GetDeviceId()
        {
            string rawTpmData = GetHeldData();
            int separator = rawTpmData.IndexOf('/') + 1;
            return "";
            //if (rawTpmData.Length > separator)
            //{
            //    return rawTpmData.Substring(separator);
            //}
            //else
            //{
            //    return GetHardwareDeviceId();
            //}
        }

        private static string GetHeldData()
        {
            TpmHandle nvUriHandle = new TpmHandle(AIOTH_PERSISTED_URI_INDEX + logicalDeviceId);
            Byte[] nvData;
            string iotHubUri = "";

            try
            {
                // Open the TPM
                Tpm2Device tpmDevice = new TbsDevice();
                tpmDevice.Connect();
                var tpm = new Tpm2(tpmDevice);

                // Read the URI from the TPM
                Byte[] name;
                NvPublic nvPublic = tpm.NvReadPublic(nvUriHandle, out name);
                nvData = tpm.NvRead(nvUriHandle, nvUriHandle, nvPublic.dataSize, 0);

                // Dispose of the TPM
                tpm.Dispose();
            }
            catch
            {
                return iotHubUri;
            }

            // Convert the data to a srting for output
            iotHubUri = System.Text.Encoding.UTF8.GetString(nvData);
            return iotHubUri;
        }

        internal static string GetHardwareDeviceName()
        {
            TpmHandle srkHandle = new TpmHandle(SRK_HANDLE);
            string hardwareDeviceId = "";
            Byte[] name;
            Byte[] qualifiedName;

            try
            {
                // Open the TPM
                Tpm2Device tpmDevice = new TbsDevice();
                tpmDevice.Connect();
                var tpm = new Tpm2(tpmDevice);

                // Read the URI from the TPM
                TpmPublic srk = tpm.ReadPublic(srkHandle, out name, out qualifiedName);

                // Dispose of the TPM
                tpm.Dispose();
            }
            catch
            {
                return hardwareDeviceId;
            }

            //// Calculate the hardware device id for this logical device
            //byte[] deviceId = CryptoLib.HashData(TpmAlgId.Sha256, BitConverter.GetBytes(logicalDeviceId), name);

            //// Produce the output string
            //foreach (byte n in deviceId)
            //{
            //    hardwareDeviceId += n.ToString("x2");
            //}
            return hardwareDeviceId;
        }





        public static void SaveValueIntoTpm(int address, byte[] data, int length, AuthValue authValue)
        {
            Tpm2Device tpmDevice;
            //if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            //{
            //    tpmDevice = new TbsDevice();
            //}
            //else
            //{
            //    tpmDevice = new LinuxTpmDevice();
            //}



            tpmDevice = Connect(true);

            tpmDevice.Connect();

            var tpm = new Tpm2(tpmDevice);

            var ownerAuth = new AuthValue();
            TpmHandle nvHandle = TpmHandle.NV(address);

            tpm[ownerAuth]._AllowErrors().NvUndefineSpace(TpmHandle.RhOwner, nvHandle);

            AuthValue nvAuth = authValue;
            var nvPublic = new NvPublic(nvHandle, TpmAlgId.Sha1, NvAttr.Authwrite | NvAttr.Authread, new byte[0], (ushort)length);
            tpm[ownerAuth].NvDefineSpace(TpmHandle.RhOwner, nvAuth, nvPublic);

            tpm[nvAuth].NvWrite(nvHandle, nvHandle, data, 0);
            tpm.Dispose();
        }

        public static byte[] ReadValueFromTpm(int address, int length, AuthValue authValue)
        {
            Tpm2Device tpmDevice;
            //if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            //{
            //    tpmDevice = new TbsDevice();
            //}
            //else
            //{
            //    tpmDevice = new LinuxTpmDevice();
            //}
            tpmDevice = Connect(true);
            tpmDevice.Connect();
            var tpm = new Tpm2(tpmDevice);
            TpmHandle nvHandle = TpmHandle.NV(address);
            AuthValue nvAuth = authValue;
            byte[] newData = tpm[nvAuth].NvRead(nvHandle, nvHandle, (ushort)length, 0);
            tpm.Dispose();
            return newData;
        }

        internal static void NVReadWrite(Tpm2 tpm)
        {
            //
            // AuthValue encapsulates an authorization value: essentially a byte-array.
            // OwnerAuth is the owner authorization value of the TPM-under-test.  We
            // assume that it (and other) auths are set to the default (null) value.
            // If running on a real TPM, which has been provisioned by Windows, this
            // value will be different. An administrator can retrieve the owner
            // authorization value from the registry.
            //
            var ownerAuth = new AuthValue();
            TpmHandle nvHandle = TpmHandle.NV(3001);

            //
            // Clean up any slot that was left over from an earlier run
            // 
            tpm._AllowErrors()
               .NvUndefineSpace(TpmRh.Owner, nvHandle);

            //
            // Scenario 1 - write and read a 32-byte NV-slot
            // 
            AuthValue nvAuth = AuthValue.FromRandom(8);
            tpm.NvDefineSpace(TpmRh.Owner, nvAuth,
                              new NvPublic(nvHandle, TpmAlgId.Sha1,
                                           NvAttr.Authread | NvAttr.Authwrite,
                                           null, 32));

            //
            // Write some data
            // 
            var nvData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            tpm.NvWrite(nvHandle, nvHandle, nvData, 0);

            //
            // And read it back
            // 
            byte[] nvRead = tpm.NvRead(nvHandle, nvHandle, (ushort)nvData.Length, 0);

            //
            // Is it correct?
            // 
            bool correct = nvData.SequenceEqual(nvRead);
            if (!correct)
            {
                throw new Exception("NV data was incorrect.");
            }

            Console.WriteLine("NV data written and read.");

            //
            // And clean up
            // 
            tpm.NvUndefineSpace(TpmRh.Owner, nvHandle);
        }

        internal static void NVCounter(Tpm2 tpm)
        {
            TpmHandle nvHandle = TpmHandle.NV(3001);
            
            tpm._AllowErrors().NvUndefineSpace(TpmRh.Owner, nvHandle);
            tpm.NvDefineSpace(TpmRh.Owner, AuthValue.FromRandom(8),
                              new NvPublic(nvHandle, TpmAlgId.Sha1,
                                           NvAttr.Counter | NvAttr.Authread | NvAttr.Authwrite,
                                           null, 8));
            
            tpm.NvIncrement(nvHandle, nvHandle);

            byte[] nvRead = tpm.NvRead(nvHandle, nvHandle, 8, 0);
            var initVal = Marshaller.FromTpmRepresentation<ulong>(nvRead);
            tpm.NvIncrement(nvHandle, nvHandle);

            nvRead = tpm.NvRead(nvHandle, nvHandle, 8, 0);
            var finalVal = Marshaller.FromTpmRepresentation<ulong>(nvRead);
            if (finalVal != initVal + 1)
            {
                throw new Exception("NV-counter fail");
            }

            Console.WriteLine("Incremented counter from {0} to {1}.", initVal, finalVal);

            tpm.NvUndefineSpace(TpmRh.Owner, nvHandle);


        } //NVCounter

        internal static void CreateTwoPrimaries(Tpm2 tpm)
        {
            var data = Encoding.UTF8.GetBytes("hello world");
            
            var handle1 = KeyHelpers.CreatePrimaryRsaKey(tpm, null, null, null, out TpmPublic key);

            IAsymSchemeUnion decScheme = new SchemeOaep(TpmAlgId.Sha1);

            var cipher = tpm.RsaEncrypt(handle1, data, decScheme, null);
            byte[] decrypted1 = tpm.RsaDecrypt(handle1, cipher, decScheme, null);

            var decyyptedData = Encoding.UTF8.GetString(decrypted1);





            var pub = tpm.ReadPublic(handle1, out byte[] name, out byte[] qn);

            var enc = KeyHelpers.CreateEncryptionDecryptionKey(tpm, handle1);

            tpm._ExpectResponses(TpmRc.Success, TpmRc.TbsCommandBlocked);
            var cipher2 = tpm.EncryptDecrypt(enc, 1, TpmAlgId.None, data, data, out byte[] test2);
            tpm.FlushContext(handle1);

            var handle2 = KeyHelpers.CreatePrimary(tpm, out TpmPublic key3); //, seed: new byte[] { 22, 123, 22, 1, 33 });
            tpm.FlushContext(handle2);



        }


        internal static void EncryptDecrypt(Tpm2 tpm)
        {
            var keyParams = KeyHelpers.CreateDecryptionKey2();

            TpmPublic pubCreated;
            CreationData creationData;
            TkCreation creationTicket;
            byte[] creationHash;


            TpmHandle h = tpm.CreatePrimary(TpmRh.Owner,
                                            null,
                                            keyParams,
                                            null, //outsideInfo,
                                            null, //new PcrSelection[] { creationPcr },
                                            out pubCreated,
                                            out creationData,
                                            out creationHash,
                                            out creationTicket);

            



        }







    }
}
