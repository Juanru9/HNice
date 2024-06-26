using System.Diagnostics;

namespace HNice.Model;

public interface IHabboRC4
{
    void SetKey(string s);
    string Encipher(string data);
    string Decipher(string data);
}
public class HabboRC4 : IHabboRC4
{
    private string[] di = new string[16];
    private long[] table = new long[256];
    private long[] key = new long[255];
    private long[] keyWindow = new long[1024];
    private long i, j;

    public void SetKey(string s)
    {
        try
        {
            Initialize();

            // Decode Key
            if (s.Length % 2 == 1)
            {
                s = s.Substring(0, s.Length - 1);
            }

            string s1 = s.Substring(0, s.Length / 2);
            string s2 = s.Substring(s.Length / 2);

            long l = 0;
            for (int idx = 0; idx < s1.Length; idx++)
            {
                string j2 = s2[idx].ToString();
                int i2 = s1.IndexOf(j2);
                long j = i2 - 1;

                if (j % 2 == 0)
                {
                    j *= 2;
                }
                if ((idx - 1) % 3 == 0)
                {
                    j *= 3;
                }
                if (j < 0)
                {
                    j = s1.Length % 2;
                }
                l += j;
                l ^= LShift(j, ((idx - 1) % 3) * 8);
            }

            i = 0;
            j = 0;

            // Initialize Public Key
            BuildTable(0);

            i = 0;
            j = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public string Encipher(string data)
    {
        string cipher = string.Empty;
        try
        {
            long t = 0;
            long k = 0;

            for (int a = 0; a < data.Length; a++)
            {
                i = (i + 1) % 256;
                j = (j + table[i]) % 256;
                t = table[i];
                table[i] = table[j];
                table[j] = t;
                k = table[(table[i] + table[j]) % 256];

                long c = data[a] ^ k;

                if (c <= 0)
                {
                    cipher += "00";
                }
                else
                {
                    cipher += di[RShift(c, 4) & 0xF];
                    cipher += di[c & 0xF];
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        return cipher;
    }

    public string Decipher(string data)
    {
        string cipher = string.Empty;
        try
        {
            long t = 0;
            long k = 0;
            int a = 0;

            while (a < data.Length)
            {
                i = (i + 1) % 256;
                j = (j + table[i]) % 256;
                t = table[i];
                table[i] = table[j];
                table[j] = t;
                k = table[(table[i] + table[j]) % 256];

                t = HexToDec(data.Substring(a, 2));
                cipher += (char)(t ^ k);
                a += 2;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        return cipher;
    }

    private long HexToDec(string data)
    {
        try
        {
            if (data.Length != 2)
            {
                throw new ArgumentException("Hex data should be exactly 2 characters.");
            }

            return Convert.ToInt64(data, 16);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return 0;
        }
    }

    private bool BuildTable(int sKey)
    {
        try
        {
            byte[] modKey = new byte[20];
            int j = 0;

            for (int i = 0; i < 20; i++)
            {
                if (j >= keyWindow.Length)
                {
                    j = 0;
                }

                modKey[i] = (byte)(sKey & keyWindow[j]);
                j++;
            }

            for (int i = 0; i < 256; i++)
            {
                table[i] = i;
            }

            j = 0;

            for (int q = 0; q < 256; q++)
            {
                j = (int)(j + table[q] + modKey[q % 20]) % 256;
                long temp = table[q];
                table[q] = table[j];
                table[j] = temp;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    // This method initializes the table and key.
    private bool Init(string sKey)
    {
        try
        {
            long keyValue = Convert.ToInt64(sKey);
            long keyLength = (keyValue & 248) / 8;

            if (keyLength < 20)
            {
                keyLength += 20;
            }

            long keyOffset = keyValue % 1024;
            long tGiven = keyValue;
            long[] w = new long[keyLength];

            for (int i = 0; i < keyLength; i++)
            {
                long tOwn = keyWindow[Math.Abs((int)(keyOffset + i) % 1024)];
                w[i] = Math.Abs(tGiven ^ tOwn);

                if (i == 31)
                {
                    tGiven = keyValue;
                }
                else
                {
                    tGiven /= 2;
                }
            }

            for (int a = 0; a < 256; a++)
            {
                key[a] = w[a % w.Length];
                table[a] = a;
            }

            long b = 0;
            long t = 0;

            for (int a = 0; a < 256; a++)
            {
                b = (b + table[a] + key[a]) % 256;
                t = table[a];
                table[a] = table[b];
                table[b] = t;
            }

            i = 0;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    // This method performs pre-mixing of the table with given data and count.
    private void PreMixTable(string data, long count)
    {
        try
        {
            for (long i = 0; i < count; i++)
            {
                string temp = Encipher(data);
            }
        }
        catch (Exception ex)
        {
            // Handle exception
        }
    }

    // This method initializes the hexadecimal string values and key window values.
    private void Initialize()
    {
        try
        {
            for (int i = 0; i < 16; i++)
            {
                switch (i)
                {
                    case 10:
                        di[i] = "A";
                        break;
                    case 11:
                        di[i] = "B";
                        break;
                    case 12:
                        di[i] = "C";
                        break;
                    case 13:
                        di[i] = "D";
                        break;
                    case 14:
                        di[i] = "E";
                        break;
                    case 15:
                        di[i] = "F";
                        break;
                    default:
                        di[i] = i.ToString();
                        break;
                }
            }

            keyWindow = new long[] {
            44, 214, 122, 91, 114, 79, 16, 141, 115, 110, 207, 216, 238, 65, 59, 50,
            186, 70, 128, 248, 107, 12, 33, 247, 66, 79, 53, 216, 74, 81, 145, 249,
            179, 111, 233, 56, 49, 92, 123, 162, 26, 46, 182, 96, 208, 93, 114, 17,
            255, 19, 164, 208, 79, 91, 241, 128, 158, 25, 252, 194, 217, 20, 22, 44,
            1, 253, 45, 91, 113, 89, 203, 80, 34, 112, 99, 82, 243, 8, 90, 240,
            39, 17, 230, 232, 80, 180, 173, 164, 112, 163, 217, 155, 170, 41, 187, 156,
            213, 199, 176, 180, 180, 236, 167, 128, 31, 155, 210, 208, 55, 198, 5, 243,
            27, 208, 78, 13, 142, 64, 80, 21, 18, 19, 175, 252, 126, 194, 11, 190,
            99, 94, 184, 248, 167, 77, 45, 5, 141, 128, 72, 42, 45, 107, 88, 140,
            147, 30, 248, 243, 208, 82, 137, 181, 69, 177, 128, 216, 25, 3, 239, 179,
            160, 159, 129, 135, 23, 62, 192, 90, 91, 172, 119, 255, 135, 39, 78, 216,
            12, 188, 45, 204, 93, 54, 30, 165, 129, 178, 151, 253, 92, 31, 196, 126,
            4, 72, 182, 180, 216, 144, 78, 255, 185, 228, 134, 92, 103, 141, 2, 144,
            123, 161, 101, 187, 145, 187, 171, 62, 21, 244, 17, 231, 203, 120, 176, 87,
            150, 89, 244, 7, 29, 21, 235, 165, 86, 125, 184, 90, 232, 232, 145, 15,
            198, 165, 103, 12, 245, 177, 151, 29, 45, 26, 184, 91, 20, 16, 231, 174,
            237, 207, 165, 251, 114, 185, 245, 68, 82, 116, 216, 0, 203, 89, 234, 174,
            100, 220, 60, 42, 60, 103, 17, 93, 208, 72, 242, 116, 148, 84, 230, 115,
            56, 138, 134, 107, 199, 17, 73, 58, 75, 187, 200, 253, 141, 249, 246, 74,
            201, 166, 194, 156, 72, 221, 20, 6, 91, 191, 243, 100, 3, 113, 79, 59,
            175, 94, 112, 81, 69, 166, 145, 89, 163, 111, 180, 110, 146, 156, 43, 206,
            248, 22, 188, 27, 123, 152, 65, 136, 212, 185, 83, 104, 162, 69, 21, 208,
            116, 78, 193, 2, 179, 222, 109, 66, 75, 56, 46, 21, 105, 140, 236, 13,
            78, 58, 30, 55, 114, 228, 96, 156, 89, 179, 116, 30, 63, 7, 52, 10,
            182, 25, 87, 29, 166, 75, 64, 89, 30, 110, 40, 50, 121, 107, 44, 151,
            246, 147, 131, 39, 105, 227, 58, 66, 56, 82, 107, 73, 91, 133, 210, 202,
            174, 56, 108, 29, 117, 109, 128, 103, 237, 227, 13, 138, 177, 180, 146, 142,
            82, 83, 115, 194, 148, 62, 74, 92, 154, 95, 194, 104, 216, 2, 166, 59,
            150, 137, 164, 49, 189, 33, 236, 46, 82, 169, 73, 77, 177, 81, 67, 98,
            181, 116, 49, 76, 97, 204, 227, 29, 203, 113, 110, 242, 255, 140, 46, 204,
            144, 39, 234, 167, 30, 150, 110, 219, 138, 136, 88, 12, 179, 71, 23, 150,
            233, 80, 217, 244, 248, 111, 65, 255, 69, 217, 55, 49, 43, 228, 225, 10,
            123, 71, 41, 173, 7, 15, 194, 8, 87, 209, 75, 212, 179, 144, 151, 48,
            134, 47, 109, 212, 8, 24, 66, 102, 198, 211, 35, 184, 154, 76, 147, 170,
            90, 247, 53, 31, 164, 5, 189, 12, 208, 99, 185, 52, 74, 154, 137, 235,
            112, 132, 5, 16, 65, 124, 87, 109, 83, 170, 37, 20, 88, 134, 2, 86,
            218, 169, 222, 128, 202, 28, 87, 81, 154, 199, 124, 239, 130, 47, 88, 219,
            61, 97, 18, 95, 81, 144, 123, 64, 49, 239, 24, 87, 134, 24, 102, 230,
            169, 145, 83, 11, 126, 166, 230, 149, 31, 164, 94, 197, 27, 225, 35, 17,
            24, 241, 140, 17, 42, 10, 40, 124, 217, 114, 116, 252, 232, 55, 77, 88,
            75, 5, 48, 180, 220, 218, 124, 97, 177, 184, 192, 205, 59, 54, 89, 152,
            79, 6, 64, 29, 167, 155, 62, 14, 197, 181, 66, 142, 153, 91, 230, 43,
            96, 110, 122, 187, 235, 209, 190, 241, 128, 50, 23, 53, 114, 43, 111, 106,
            99, 15, 232, 115, 101, 210, 234, 245, 238, 164, 56, 123, 94, 125, 223, 97,
            210, 151, 91, 204, 4, 72, 140, 41, 143, 19, 93, 212, 153, 102, 182, 243,
            102, 93, 214, 32, 68, 236, 146, 92, 168, 99, 46, 150, 249, 34, 177, 203,
            105, 126, 129, 43, 156, 166, 3, 168, 43, 81, 183, 131, 168, 111, 131, 157,
            155, 195, 195, 177, 47, 180, 82, 61, 225, 62, 150, 176, 212, 191, 129, 117,
            98, 72, 173, 192, 36, 203, 15, 224, 254, 52, 127, 174, 231, 38, 213, 239,
            120, 52, 178, 101, 97, 132, 130, 144, 152, 251, 226, 90, 18, 233, 74, 41,
            88, 28, 17, 58, 177, 84, 226, 119, 241, 25, 192, 7, 157, 125, 170, 188,
            191, 186, 75, 97, 225, 115, 184, 100, 168, 133, 0, 220, 95, 160, 242, 14,
            185, 219, 214, 108, 157, 142, 32, 135, 69, 86, 64, 90, 236, 179, 137, 64,
            128, 214, 63, 132, 152, 177, 167, 158, 8, 122, 139, 89, 115, 11, 27, 85,
            94, 45, 12, 164, 18, 169, 213, 74, 196, 61, 55, 60, 238, 33, 77, 181,
            88, 166, 61, 96, 152, 139, 209, 42, 223, 203, 149, 25, 93, 71, 132, 40,
            77, 31, 187, 168, 88, 210, 106, 251, 181, 29, 15, 158, 194, 183, 176, 230,
            91, 2, 124, 174, 86, 165, 57, 108, 191, 227, 106, 164, 159, 110, 35, 205,
            248, 254, 105, 129, 25, 77, 6, 164, 93, 176, 192, 205, 26, 96, 109, 191,
            35, 239, 46, 124, 53, 208, 221, 175, 169, 246, 68, 228, 158, 39, 221, 66,
            234, 170, 154, 6, 192, 132, 25, 6, 168, 169, 26, 251, 183, 23, 204, 192,
            34, 96, 126, 20, 183, 135, 20, 223, 115, 137, 254, 247, 13, 71, 7, 176,
            162, 184, 184, 255, 128, 229, 236, 107, 42, 80, 68, 112, 127, 4, 57, 89,
            26, 78, 251, 177, 21, 151, 224, 26, 227, 112, 78, 240, 11, 247, 87, 103
        };

            i = 0;
            j = 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    // This method shifts the bits of a value left or right.
    private long Shift(long lValue, long lNumberOfBitsToShift, bool lDirectionToShift)
    {
        try
        {
            if (lDirectionToShift)
            {
                return lValue * (1 << (int)lNumberOfBitsToShift);
            }
            else
            {
                return lValue / (1 << (int)lNumberOfBitsToShift);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return 0;
        }
    }

    private long LShift(long lValue, long lNumberOfBitsToShift)
    {
        return Shift(lValue, lNumberOfBitsToShift, true);
    }

    private long RShift(long lValue, long lNumberOfBitsToShift)
    {
        return Shift(lValue, lNumberOfBitsToShift, false);
    }
}
