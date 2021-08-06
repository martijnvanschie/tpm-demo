using System;
using System.Text;
using Tpm2Lib;

namespace TpmEncryptor
{
    class Program
    {
        static IAsymSchemeUnion decScheme = new SchemeOaep(TpmAlgId.Sha1);

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            try
            {
                using (var client = TpmClient.CreateSimulatorClient())
                {
                    //var endAuth = client.Tpm.EndorsementAuth;

                    //AuthSession s = client.Tpm.StartAuthSessionEx(TpmSe.Hmac, TpmAlgId.Sha256, SessionAttr.ContinueSession);

                    //client.Tpm.OwnerAuth = new AuthValue(new byte[] { 1, 2, 3 });

                    //client.Tpm._Behavior.Strict = true;

                    var helloWorld = Encoding.UTF8.GetBytes("Hello World");

                    var cipher = RsaEncrypt(client.Tpm[Auth.Default], helloWorld);
                    var decipher = RsaDecrypt(client.Tpm[Auth.Default], cipher);

                    var helloWorld2 = Encoding.UTF8.GetString(decipher);

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(500);
            }

            Environment.Exit(0);
        }


        internal static byte[] RsaEncrypt(Tpm2 tpm, byte[] data)
        {
            var handle1 = KeyHelpers.CreatePrimaryRsaKey(tpm, null, new byte[] { 1 }, new byte[] { 1, 2, 3 }, out TpmPublic key);
            var cipher = tpm.RsaEncrypt(handle1, data, decScheme, null);
            tpm.FlushContext(handle1);
            return cipher;
        }

        internal static byte[] RsaDecrypt(Tpm2 tpm, byte[] data)
        {
            var handle1 = KeyHelpers.CreatePrimaryRsaKey(tpm, null, new byte[] { 2 }, new byte[] { 1, 2, 3 }, out TpmPublic key);
            byte[] decrypted1 = tpm.RsaDecrypt(handle1, data, decScheme, null);
            tpm.FlushContext(handle1);
            return decrypted1;
        }
    }
}
