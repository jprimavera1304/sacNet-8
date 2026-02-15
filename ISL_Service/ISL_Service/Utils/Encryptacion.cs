using System.Security.Cryptography;

namespace ISL_Service.Utils
{
    public class Encryptacion
    {

        //static public string appPwdUnique = "AquiPuedesPonerElTextoQueDeseesComoClaveUnica";
        private const string appPwdUnique2 = "AquiPuedesPonerElTextoQueDeseesComoClaveUnica";
        private const string appPwdUniqueAccent = "AquíPuedesPonerElTextoQueDeseesComoClaveUnica";

        #region "Encrypt"
        private byte[] Encrypt(byte[] clearData, byte[] Key, byte[] IV)
        {
            MemoryStream ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();
            alg.Key = Key;
            alg.IV = IV;
            CryptoStream cs = new CryptoStream(ms, alg.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(clearData, 0, clearData.Length);
            cs.Close();
            byte[] encryptedData = ms.ToArray();
            return encryptedData;
        }
        #endregion

        #region "Encrypt"
        public string Encrypt(string Data, string Password = appPwdUnique2, int Bits = 256)
        {
            byte[] clearBytes = System.Text.Encoding.Unicode.GetBytes(Data);
            PasswordDeriveBytes pdb = new PasswordDeriveBytes(Password, new byte[] { 0x0, 0x1, 0x2, 0x1C, 0x1D, 0x1E, 0x3, 0x4, 0x5, 0xF, 0x20, 0x21, 0xAD, 0xAF, 0xA4 });
            if (Bits == 128)
            {
                byte[] encryptedData = Encrypt(clearBytes, pdb.GetBytes(16), pdb.GetBytes(16));
                return Convert.ToBase64String(encryptedData);
            }
            else if (Bits == 192)
            {
                byte[] encryptedData = Encrypt(clearBytes, pdb.GetBytes(24), pdb.GetBytes(16));
                return Convert.ToBase64String(encryptedData);
            }
            else if (Bits == 256)
            {
                byte[] encryptedData = Encrypt(clearBytes, pdb.GetBytes(32), pdb.GetBytes(16));
                return Convert.ToBase64String(encryptedData);
            }
            else
            {
                return String.Concat(Bits);
            }

        }
        #endregion

        #region "Decrypt"
        private byte[] Decrypt(byte[] cipherData, byte[] Key, byte[] IV)
        {
            MemoryStream ms = new MemoryStream();
            Rijndael alg = Rijndael.Create();
            alg.Key = Key;
            alg.IV = IV;
            CryptoStream cs = new CryptoStream(ms, alg.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(cipherData, 0, cipherData.Length);
            cs.Close();
            byte[] decryptedData = ms.ToArray();
            return decryptedData;
        }
        #endregion

        #region "Decrypt"
        public string Decrypt(string Data, string Password = appPwdUnique2, int Bits = 256)
        {
            var passwords = new List<string>();
            if (!string.IsNullOrWhiteSpace(Password))
            {
                passwords.Add(Password);
            }

            // Compatibilidad permanente con ambos esquemas históricos de clave.
            if (!passwords.Contains(appPwdUniqueAccent)) passwords.Add(appPwdUniqueAccent);
            if (!passwords.Contains(appPwdUnique2)) passwords.Add(appPwdUnique2);

            foreach (var candidate in passwords)
            {
                if (TryDecryptWithPassword(Data, candidate, Bits, out var decrypted))
                {
                    return decrypted;
                }
            }

            return "";
        }
        #endregion

        private bool TryDecryptWithPassword(string data, string password, int bits, out string result)
        {
            result = "";
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(data);
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(password, new byte[] { 0x0, 0x1, 0x2, 0x1C, 0x1D, 0x1E, 0x3, 0x4, 0x5, 0xF, 0x20, 0x21, 0xAD, 0xAF, 0xA4 });

                byte[] decryptedData;
                if (bits == 128)
                {
                    decryptedData = Decrypt(cipherBytes, pdb.GetBytes(16), pdb.GetBytes(16));
                }
                else if (bits == 192)
                {
                    decryptedData = Decrypt(cipherBytes, pdb.GetBytes(24), pdb.GetBytes(16));
                }
                else if (bits == 256)
                {
                    decryptedData = Decrypt(cipherBytes, pdb.GetBytes(32), pdb.GetBytes(16));
                }
                else
                {
                    result = String.Concat(bits);
                    return true;
                }

                result = System.Text.Encoding.Unicode.GetString(decryptedData);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
