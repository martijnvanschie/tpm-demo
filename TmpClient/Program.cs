using System;
using System.Collections.Generic;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    class Program
    {
        static Tpm2Device _tpmDevice;

        static bool useSimulator = true;

        /// <summary>
        /// If using a TCP connection, the default DNS name/IP address for the
        /// simulator.
        /// </summary>
        private const string DefaultSimulatorName = "127.0.0.1";

        /// <summary>
        /// If using a TCP connection, the default TCP port of the simulator.
        /// </summary>
        private const int DefaultSimulatorPort = 2321;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Examples.ConnectLocal();
            Examples.ConnectSimulator();


            using (var device = Examples.Connect(useSimulator))
            {
                Examples.AvailablePCRBanks(device);
            }



                return;



            ReadPcr();
            Sign();

            try
            {
                using (Tpm2Device tpmDevice = new TbsDevice())
                {
                    tpmDevice.Connect();

                    using (var tpm = new Tpm2(tpmDevice))
                    {

                        ICapabilitiesUnion caps;
                        tpm.GetCapability(Cap.Algs, 0, 1000, out caps);
                        var algsx = (AlgPropertyArray)caps;

                        Console.WriteLine("Supported algorithms:");
                        foreach (var alg in algsx.algProperties)
                        {
                            Console.WriteLine("  {0}", alg.alg.ToString());
                        }

                        Console.WriteLine("Supported commands:");
                        tpm.GetCapability(Cap.TpmProperties, (uint)Pt.TotalCommands, 1, out caps);
                        tpm.GetCapability(Cap.Commands, (uint)TpmCc.First, TpmCc.Last - TpmCc.First + 1, out caps);

                        var commands = (CcaArray)caps;
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

                        //
                        // As an alternative: call GetCapabilities more than once to obtain all values
                        //
                        byte more;
                        var firstCommandCode = (uint)TpmCc.First;
                        do
                        {
                            more = tpm.GetCapability(Cap.Commands, firstCommandCode, 10, out caps);
                            commands = (CcaArray)caps;
                            //
                            // Commands are sorted; getting the last element as it will be the largest.
                            //
                            uint lastCommandCode = (uint)commands.commandAttributes[commands.commandAttributes.Length - 1] & 0x0000FFFFU;
                            firstCommandCode = lastCommandCode;
                        } while (more == 1);

                        //
                        // Read PCR attributes. Cap.Pcrs returns the list of PCRs which are supported
                        // in different PCR banks. The PCR banks are identified by the hash algorithm
                        // used to extend values into the PCRs of this bank.
                        // 
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.ReadKey();
        }

        static void ConnectSimulator()
        {
            _tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
        }

        static void ReadPcr()
        {
            Console.WriteLine("\nPCR sample started.");

            using (Tpm2Device tpmDevice = new TbsDevice())
            {
                tpmDevice.Connect();

                using (var tpm = new Tpm2(tpmDevice))
                {
                    var valuesToRead = new PcrSelection[]
                    {
                        new PcrSelection(TpmAlgId.Sha1, new uint[] {1, 2})
                    };

                    PcrSelection[] valsRead;
                    Tpm2bDigest[] values;

                    tpm.PcrRead(valuesToRead, out valsRead, out values);

                    if (valsRead[0] != valuesToRead[0])
                    {
                        Console.WriteLine("Unexpected PCR-set");
                    }

                    var pcr1 = new TpmHash(TpmAlgId.Sha1, values[0].buffer);
                    Console.WriteLine("PCR1: " + pcr1);

                    var dataToExtend = new byte[] { 0, 1, 2, 3, 4 };
                    tpm.PcrEvent(TpmHandle.Pcr(1), dataToExtend);
                    tpm.PcrRead(valuesToRead, out valsRead, out values);
                }
            }
        }

        static void Sign()
        {
            using (Tpm2Device tpmDevice = new TbsDevice())
            {
                tpmDevice.Connect();

                using (var tpm = new Tpm2(tpmDevice))
                {
                    var keyTemplate = new TpmPublic(TpmAlgId.Sha1,                                  // Name algorithm
                                 ObjectAttr.UserWithAuth | ObjectAttr.Sign |     // Signing key
                                 ObjectAttr.FixedParent | ObjectAttr.FixedTPM | // Non-migratable 
                                 ObjectAttr.SensitiveDataOrigin,
                                 null,                                    // No policy
                                 new RsaParms(new SymDefObject(),
                                              new SchemeRsassa(TpmAlgId.Sha1), 2048, 0),
                                 new Tpm2bPublicKeyRsa());

                    var ownerAuth = new AuthValue();

                    var keyAuth = new byte[] { 1, 2, 3 };

                    TpmPublic keyPublic;
                    CreationData creationData;
                    TkCreation creationTicket;
                    byte[] creationHash;

                    TpmHandle keyHandle = tpm[ownerAuth].CreatePrimary(
                                TpmRh.Owner,                            // In the owner-hierarchy
                                new SensitiveCreate(keyAuth, null),     // With this auth-value
                                keyTemplate,                            // Describes key
                                null,                                   // Extra data for creation ticket
                                new PcrSelection[0],                    // Non-PCR-bound
                                out keyPublic,                          // PubKey and attributes
                                out creationData, out creationHash, out creationTicket);    // Not used here

                    // 
                    // Print out text-versions of the public key just created
                    // 
                    Console.WriteLine("New public key\n" + keyPublic.ToString());

                    byte[] message = Encoding.Unicode.GetBytes("ABC");
                    TpmHash digestToSign = TpmHash.FromData(TpmAlgId.Sha1, message);

                    var signature = tpm[keyAuth].Sign(keyHandle,            // Handle of signing key
                                  digestToSign,         // Data to sign
                                  null,                 // Use key's scheme
                                 TpmHashCheck.Null()) as SignatureRsassa;
                    // 
                    // Print the signature.
                    // 
                    Console.WriteLine("Signature: " + BitConverter.ToString(signature.sig));


                    bool sigOk = keyPublic.VerifySignatureOverData(message, signature);
                    if (!sigOk)
                    {
                        throw new Exception("Signature did not validate.");
                    }

                    Console.WriteLine("Verified signature with TPM2lib (software implementation).");

                    TpmHandle pubHandle = tpm.LoadExternal(null, keyPublic, TpmRh.Owner);
                    tpm.VerifySignature(pubHandle, digestToSign, signature);
                    Console.WriteLine("Verified signature with TPM.");
                }
            }
        }
    }
}
