using System;
using System.Collections.Generic;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    internal class RsaExamples
    {
        static IAsymSchemeUnion decScheme = new SchemeOaep(TpmAlgId.Sha1);

        internal static byte[] RsaEncrypt(Tpm2 tpm, byte[] data)
        {
            var handle1 = KeyHelpers.CreatePrimaryRsaKey(tpm, null, new byte[] { 1 }, new byte[] { 1,2,3 }, out TpmPublic key);
            var cipher = tpm.RsaEncrypt(handle1, data, decScheme, null);
            tpm.FlushContext(handle1);
            return cipher;
        }

        internal static byte[] RsaDecrypt(Tpm2 tpm, byte[] data)
        {
            var handle1 = KeyHelpers.CreatePrimaryRsaKey(tpm, null, new byte[] { 2 }, new byte[] { 1,2,3 }, out TpmPublic key);
            byte[] decrypted1 = tpm.RsaDecrypt(handle1, data, decScheme, null);
            tpm.FlushContext(handle1);
            return decrypted1;
        }
    }
}
