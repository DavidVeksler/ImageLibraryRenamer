using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.UI;

namespace ImageLibraryRenamer.ExifExtractor
{
    namespace EXIF
    {
        /// <summary>
        ///     EXIFextractor Class
        /// </summary>
        public class EXIFextractor : IEnumerable
        {
            //
            private readonly Bitmap bmp;
            //
            //
            private readonly translation myHash;
            //
            private readonly Hashtable properties;
            //

            //
            private readonly string sp;
            private string data;
            private string msp = "";

            /// <summary>
            /// </summary>
            /// <param name="bmp"></param>
            /// <param name="sp"></param>
            public EXIFextractor(ref Bitmap bmp, string sp)
            {
                properties = new Hashtable();
                //
                this.bmp = bmp;
                this.sp = sp;
                //
                myHash = new translation();
                buildDB(this.bmp.PropertyItems);
            }

            public EXIFextractor(ref Bitmap bmp, string sp, string msp)
            {
                properties = new Hashtable();
                this.sp = sp;
                this.msp = msp;
                this.bmp = bmp;
                //				
                myHash = new translation();
                buildDB(bmp.PropertyItems);
            }

            public EXIFextractor(string file, string sp, string msp)
            {
                properties = new Hashtable();
                this.sp = sp;
                this.msp = msp;

                myHash = new translation();
                //				
                buildDB(GetExifProperties(file));
            }

            
            public object this[string index]
            {
                get { return properties[index]; }
            }

            internal int Count
            {
                get { return properties.Count; }
            }

            /// <summary>
            /// </summary>
            /// <param name="id"></param>
            /// <param name="len"></param>
            /// <param name="type"></param>
            /// <param name="data"></param>
            public void setTag(int id, string data)
            {
                Encoding ascii = Encoding.ASCII;
                setTag(id, data.Length, 0x2, ascii.GetBytes(data));
            }

            /// <summary>
            /// </summary>
            /// <param name="id"></param>
            /// <param name="len"></param>
            /// <param name="type"></param>
            /// <param name="data"></param>
            public void setTag(int id, int len, short type, byte[] data)
            {
                PropertyItem p = CreatePropertyItem(type, id, len, data);
                bmp.SetPropertyItem(p);
                buildDB(bmp.PropertyItems);
            }

            /// <summary>
            /// </summary>
            /// <param name="type"></param>
            /// <param name="tag"></param>
            /// <param name="len"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            private static PropertyItem CreatePropertyItem(short type, int tag, int len, byte[] value)
            {
                PropertyItem item;

                // Loads a PropertyItem from a Jpeg image stored in the assembly as a resource.
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream emptyBitmapStream = assembly.GetManifestResourceStream("EXIFextractor.decoy.jpg");
                Image empty = Image.FromStream(emptyBitmapStream);

                item = empty.PropertyItems[0];

                // Copies the data to the property item.
                item.Type = type;
                item.Len = len;
                item.Id = tag;
                item.Value = new byte[value.Length];
                value.CopyTo(item.Value, 0);

                return item;
            }

            public static PropertyItem[] GetExifProperties(string fileName)
            {
                FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                Image image = Image.FromStream(stream,
                    /* useEmbeddedColorManagement = */ true,
                    /* validateImageData = */ false);
                return image.PropertyItems;
            }

            /// <summary>
            /// </summary>
            private void buildDB(PropertyItem[] parr)
            {
                properties.Clear();
                //
                data = "";
                //
                Encoding ascii = Encoding.ASCII;
                //
                foreach (PropertyItem p in parr)
                {
                    try
                    {
                        string v = "";
                        string name = (string)myHash[p.Id];
                        // tag not found. skip it
                        if (name == null) continue;
                        //
                        data += name + ": ";
                        //
                        //1 = BYTE An 8-bit unsigned integer.,
                        switch (p.Type)
                        {
                            case 0x1:
                                v = p.Value[0].ToString();
                                break;
                            case 0x2:
                                v = ascii.GetString(p.Value);
                                break;
                            case 0x3:
                                switch (p.Id)
                                {
                                    default:
                                        v = convertToInt16U(p.Value).ToString();
                                        break;
                                }
                                break;
                            case 0x4:
                                v = convertToInt32U(p.Value).ToString();
                                break;
                            case 0x5:
                                {
                                    // rational
                                    byte[] n = new byte[p.Len / 2];
                                    byte[] d = new byte[p.Len / 2];
                                    Array.Copy(p.Value, 0, n, 0, p.Len / 2);
                                    Array.Copy(p.Value, p.Len / 2, d, 0, p.Len / 2);
                                    uint a = convertToInt32U(n);
                                    uint b = convertToInt32U(d);
                                    Rational r = new Rational(a, b);
                                    //
                                    //convert here
                                    //
                                    switch (p.Id)
                                    {
                                        case 0x9202: // aperture
                                            v = "F/" + Math.Round(Math.Pow(Math.Sqrt(2), r.ToDouble()), 2).ToString();
                                            break;
                                        case 0x920A:
                                            v = r.ToDouble().ToString();
                                            break;
                                        case 0x829A:
                                            v = r.ToDouble().ToString();
                                            break;
                                        case 0x829D: // F-number
                                            v = "F/" + r.ToDouble().ToString();
                                            break;
                                        default:
                                            v = r.ToString("/");
                                            break;
                                    }
                                }
                                break;
                            case 0x7:
                                switch (p.Id)
                                {
                                    case 0xA300:
                                        {
                                            if (p.Value[0] == 3)
                                            {
                                                v = "DSC";
                                            }
                                            else
                                            {
                                                v = "reserved";
                                            }
                                            break;
                                        }
                                    default:
                                        v = "-";
                                        break;
                                }
                                break;
                            case 0x9:
                                v = convertToInt32(p.Value).ToString();
                                break;
                            case 0xA:
                                {
                                    // rational
                                    byte[] n = new byte[p.Len / 2];
                                    byte[] d = new byte[p.Len / 2];
                                    Array.Copy(p.Value, 0, n, 0, p.Len / 2);
                                    Array.Copy(p.Value, p.Len / 2, d, 0, p.Len / 2);
                                    int a = convertToInt32(n);
                                    int b = convertToInt32(d);
                                    Rational r = new Rational(a, b);
                                    //
                                    // convert here
                                    //
                                    switch (p.Id)
                                    {
                                        case 0x9201: // shutter speed
                                            v = "1/" + Math.Round(Math.Pow(2, r.ToDouble()), 2).ToString();
                                            break;
                                        case 0x9203:
                                            v = Math.Round(r.ToDouble(), 4).ToString();
                                            break;
                                        default:
                                            v = r.ToString("/");
                                            break;
                                    }
                                }
                                break;
                        }
                        // add it to the list
                        if (properties[name] == null)
                            properties.Add(name, v);
                        // cat it too
                        data += v;
                        data += sp;


                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }

            /// <summary>
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return data;
            }

            /// <summary>
            /// </summary>
            /// <param name="arr"></param>
            /// <returns></returns>
            private int convertToInt32(byte[] arr)
            {
                if (arr.Length != 4)
                    return 0;
                else
                    return arr[3] << 24 | arr[2] << 16 | arr[1] << 8 | arr[0];
            }

            /// <summary>
            /// </summary>
            /// <param name="arr"></param>
            /// <returns></returns>
            private int convertToInt16(byte[] arr)
            {
                if (arr.Length != 2)
                    return 0;
                else
                    return arr[1] << 8 | arr[0];
            }

            /// <summary>
            /// </summary>
            /// <param name="arr"></param>
            /// <returns></returns>
            private uint convertToInt32U(byte[] arr)
            {
                if (arr.Length != 4)
                    return 0;
                else
                    return Convert.ToUInt32(arr[3] << 24 | arr[2] << 16 | arr[1] << 8 | arr[0]);
            }

            /// <summary>
            /// </summary>
            /// <param name="arr"></param>
            /// <returns></returns>
            private uint convertToInt16U(byte[] arr)
            {
                if (arr.Length != 2)
                    return 0;
                else
                    return Convert.ToUInt16(arr[1] << 8 | arr[0]);
            }

            #region IEnumerable Members

            public IEnumerator GetEnumerator()
            {
                // TODO:  Add EXIFextractor.GetEnumerator implementation
                return (new EXIFextractorEnumerator(properties));
            }

            #endregion
        }

        //
        // dont touch this class. its for IEnumerator
        // 
        //
        internal class EXIFextractorEnumerator : IEnumerator
        {
            private Hashtable exifTable;
            private IDictionaryEnumerator index;

            internal EXIFextractorEnumerator(Hashtable exif)
            {
                exifTable = exif;
                Reset();
                index = exif.GetEnumerator();
            }

            #region IEnumerator Members

            public void Reset()
            {
                index = null;
            }

            public object Current
            {
                get { return (new Pair(index.Key, index.Value)); }
            }

            public bool MoveNext()
            {
                if (index != null && index.MoveNext())
                    return true;
                else
                    return false;
            }

            #endregion
        }

        /// <summary>
        ///     Summary description for translation.
        /// </summary>
        public sealed class translation : Hashtable
        {
            /// <summary>
            /// </summary>
            public translation()
            {
                Add(0x132, "Date Time");
            }
        }

        /// <summary>
        ///     private class
        /// </summary>
        internal class Rational
        {
            private readonly int d;
            private readonly int n;

            public Rational(int n, int d)
            {
                this.n = n;
                this.d = d;
                simplify(ref this.n, ref this.d);
            }

            public Rational(uint n, uint d)
            {
                this.n = Convert.ToInt32(n);
                this.d = Convert.ToInt32(d);

                simplify(ref this.n, ref this.d);
            }

            public Rational()
            {
                n = d = 0;
            }

            public string ToString(string sp)
            {
                if (sp == null) sp = "/";
                return n.ToString() + sp + d.ToString();
            }

            public double ToDouble()
            {
                if (d == 0)
                    return 0.0;

                return Math.Round(Convert.ToDouble(n) / Convert.ToDouble(d), 2);
            }

            private void simplify(ref int a, ref int b)
            {
                if (a == 0 || b == 0)
                    return;

                int gcd = euclid(a, b);
                a /= gcd;
                b /= gcd;
            }

            private int euclid(int a, int b)
            {
                if (b == 0)
                    return a;
                else
                    return euclid(b, a % b);
            }
        }
    }
}
