using System;
using System.Collections.Generic;
using System.Text;
using Tpm2Lib;

namespace TmpClient
{
    internal class KeyHelpers
    {
        internal static TpmHandle CreatePrimaryRsaKey(Tpm2 tpm, TpmHandle primHandle, byte[] auth, byte[] seed, out TpmPublic keyPublic)
        {
            if (primHandle == null)
            {
                primHandle = TpmRh.Endorsement;
            }

            TpmPublic keyInPublic = new TpmPublic(TpmAlgId.Sha1,
                                                  ObjectAttr.Decrypt | ObjectAttr.Sign | ObjectAttr.FixedParent | ObjectAttr.FixedTPM
                                                      | ObjectAttr.UserWithAuth | ObjectAttr.SensitiveDataOrigin,
                                                  null,
                                                  new RsaParms(
                                                      new SymDefObject(),
                                                      new NullAsymScheme(),
                                                      2048, 0),
                                                  new Tpm2bPublicKeyRsa(seed));

            SensitiveCreate sensCreate = new SensitiveCreate(auth, null);

            CreationData keyCreationData;
            TkCreation creationTicket;
            byte[] creationHash;

            TpmHandle keyPrivate = tpm.CreatePrimary(primHandle,
                                                     sensCreate,
                                                     keyInPublic,
                                                     auth,
                                                     new PcrSelection[0],
                                                     out keyPublic,
                                                     out keyCreationData,
                                                     out creationHash,
                                                     out creationTicket);

            return keyPrivate;
        }

        internal static TpmHandle CreatePrimary(Tpm2 tpm, out TpmPublic newKeyPub, byte[] auth = null, byte[] seed = null)
        {
            var sensCreate = new SensitiveCreate(auth, null);

            var parms = new TpmPublic(TpmAlgId.Sha256,                   // Name algorithm
                          ObjectAttr.Restricted | ObjectAttr.Decrypt |   // Storage key
                          ObjectAttr.FixedParent | ObjectAttr.FixedTPM |   // Non-duplicable
                          ObjectAttr.UserWithAuth | ObjectAttr.SensitiveDataOrigin,
                          null,                                             // No policy
                                                                            // No signing or decryption scheme, and non-empty symmetric
                                                                            // specification (even when it is an asymmetric key)
                          new RsaParms(new SymDefObject(TpmAlgId.Aes, 128, TpmAlgId.Cfb),
                                       null, 2048, 0),
                          new Tpm2bPublicKeyRsa(seed)     // Additional entropy for key derivation
                        );

            CreationData creationData;
            TkCreation creationTicket;
            byte[] creationHash;

            var handle = tpm.CreatePrimary(TpmRh.Owner,          // In storage hierarchy
                                      sensCreate,           // Auth value
                                      CreateDecryptionKey(),                // Key template
                                                            //
                                                            // The following parameters influence the creation of the 
                                                            // creation-ticket. They are not used in this sample
                                                            //
                                      null,                 // Null outsideInfo
                                      new PcrSelection[0],  // Not PCR-bound
                                      out newKeyPub,        // Our outs
                                      out creationData, out creationHash, out creationTicket);

            return handle;
        }

        internal static TpmPublic CreateDecryptionKey()
        {
            var sym = new SymDefObject(TpmAlgId.Aes, 128, TpmAlgId.Cfb);

            var pub = new TpmPublic(TpmAlgId.Sha256,
                            ObjectAttr.Decrypt | ObjectAttr.UserWithAuth,
                            null,
                            new SymDefObject(TpmAlgId.Aes, 128, TpmAlgId.Cfb),
                            new Tpm2bDigestSymcipher());

            return pub;
        }

        internal static TpmPublic CreateDecryptionKey2()
        {
            var pub = new TpmPublic(TpmAlgId.Sha1,
                                    ObjectAttr.Decrypt | ObjectAttr.UserWithAuth | ObjectAttr.SensitiveDataOrigin,
                                    null,
                                    new RsaParms(null, new SchemeOaep(TpmAlgId.Sha1), 2048, 0),
                                    new Tpm2bPublicKeyRsa());
            return pub;
        }

        internal static TpmHandle CreateEncryptionDecryptionKey(Tpm2 tpm, TpmHandle parent)
        {
            var sensCreate = new SensitiveCreate(null, null);

            var sym = new SymDefObject(TpmAlgId.Aes, 128, TpmAlgId.Cfb);

            var pub = new TpmPublic(TpmAlgId.Sha256,
                            ObjectAttr.Decrypt | ObjectAttr.UserWithAuth,
                            null,
                            sym,
                            new Tpm2bDigestSymcipher());

            TssObject swKey = TssObject.Create(pub);


            var innerWrapKey = sym == null ? null : SymCipher.Create(sym);



            byte[] name, qname;
            TpmPublic pubParent = tpm.ReadPublic(parent, out name, out qname);

            byte[] encSecret;
            TpmPrivate dupBlob = swKey.GetDuplicationBlob(pubParent, innerWrapKey, out encSecret);

            TpmPrivate privImp = tpm.Import(parent, innerWrapKey, swKey.Public, dupBlob,
                                encSecret, sym ?? new SymDefObject());

            TpmHandle hKey = tpm.Load(parent, privImp, swKey.Public)
                                .SetAuth(swKey.Sensitive.authValue);

            return hKey;
        }
    }
}
