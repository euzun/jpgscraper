using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegClassifier
{
    class Program
    {
        static int chunk_size = 4 * 1024;
        static string possible_fnames = "validImgList.txt";//put your candidate file names into this file.
        static StreamWriter fs_available = new StreamWriter("jpeg_available_files_4K.txt");
        static StreamWriter fs_analyzed = new StreamWriter("jpeg_analyzed_files_4K.txt");
        static StreamWriter fs_result = new StreamWriter("jpeg_result_4K.txt");
        static Random rnd = new Random();
        static void Main(string[] args)
        {
            //main_new();
            main_jpeg();
            //string prefixedHex = "0x0C030100";
            //int intValue = Convert.ToInt32(prefixedHex, 16);
            //Console.WriteLine(intValue);
            //Console.WriteLine((byte)intValue);
            //Console.WriteLine(((uint)intValue << 25));

            ////try partial search
            //FileStream fileStream = new FileStream(@"C:\Users\pala\Desktop\erkam\partial", FileMode.Open);
            //get_cnt_sos(fileStream);
            //fileStream.Close();


            Console.Read();
        }

        static void main_new()
        {
            string fname;
            string extension;
            List<string> flist = new List<string>();
            StreamReader sr = new StreamReader(possible_fnames);
            char[] delim1 = { '\\', '.' };
            char[] delim2 = { '.' };

            Dictionary<string, int> ftype_dict = new Dictionary<string, int>();
            int dict_val;
            while ((fname = sr.ReadLine()) != null)
            {
                try
                {
                    FileStream fileStream = new FileStream(fname, FileMode.Open);
                    if (fname.Contains(".") && (fileStream.Length >= chunk_size))
                    {
                        fileStream.Close();
                        fname=fname.ToLower();
                        //extension = fname.Split(delim1).Last().Split(delim2).Last();
                        flist.Add(fname);
                        fs_available.WriteLine(fname);
                    }
                }
                catch (Exception e) { }
            }
            fs_available.Close();
            flist = ShuffleList(flist);
            for (int i = 0; i < flist.Count; i++)
            {
                fname = flist[i];
                try
                {
                    extension = fname.Split(delim1).Last().Split(delim2).Last();
                    if ( !(extension.Equals("jpg") || extension.Equals("jpeg")) )
                    {
                        if (ftype_dict.ContainsKey(extension))
                        {
                            ftype_dict.TryGetValue(extension, out dict_val);
                            if (dict_val < 100)
                            {
                                ftype_dict[extension] = dict_val + 1;
                                process_file(fname, extension);
                            }

                        }
                        else
                        {
                            ftype_dict.Add(extension, 1);
                            process_file(fname, extension);
                        }
                    }
                    
                }
                catch (Exception e) { }
            }
        }

        static void main_jpeg()
        {
            string fname;
            string extension;
            List<string> flist = new List<string>();
            StreamReader sr = new StreamReader(possible_fnames);
            int cnt = 0;
            while ((fname = sr.ReadLine()) != null && cnt<200000)
            {
                flist.Add(fname);
                cnt++;
            }
            //flist = ShuffleList(flist);
            for (int i = 0; i < flist.Count; i++)
            {
                fname = flist[i];
                try
                {
                    extension = "jpg";
                    process_jpeg(fname, extension);
                }
                catch (Exception e) { }
            }
            fs_analyzed.Close();
            fs_result.Close();
        }

        static List<E> ShuffleList<E>(List<E> inputList)
        {
            List<E> randomList = new List<E>();

            Random r = new Random();
            int randomIndex = 0;
            while (inputList.Count > 0)
            {
                randomIndex = r.Next(0, inputList.Count); //Choose a random object in the list
                randomList.Add(inputList[randomIndex]); //add it to the new, random list
                inputList.RemoveAt(randomIndex); //remove to avoid duplicates
            }

            return randomList; //return the new random list
        }

        static void process_file(string fname,string extension)
        {
            fs_analyzed.WriteLine(fname);
            FileStream fileStream = new FileStream(fname, FileMode.Open);
            for (int i=0;i<10;i++)
            {
                string line = extension + ":";
                int offset = (fileStream.Length - chunk_size > int.MaxValue) ? rnd.Next(0, int.MaxValue) : rnd.Next(0, (int)fileStream.Length - chunk_size);
                fileStream.Seek(offset, SeekOrigin.Begin);

                line += get_cnt_rem_bnd(fileStream) + ":";
                fileStream.Seek(offset, SeekOrigin.Begin);
                line += search_ff00_ffda_rst(fileStream) + ":";
                fileStream.Seek(offset, SeekOrigin.Begin);
                line += get_cnt_sos(fileStream);
                Console.WriteLine(line);
                fs_result.WriteLine(line);
            }
            fileStream.Close();
        }

        static void process_jpeg(string fname, string extension)
        {
            fs_analyzed.WriteLine(fname);
            FileStream fileStream = new FileStream(fname, FileMode.Open);
            var sos=get_sos_cnt_and_point(fileStream);
            string sos_cnt = sos.Item1;
            long sos_point = sos.Item2;

            string line = extension + ":";
            long offset = (fileStream.Length - chunk_size- sos_point-10 > int.MaxValue) ? rnd.Next(0, int.MaxValue) : rnd.Next(0, (int)fileStream.Length - chunk_size-(int)sos_point-10);
            offset += sos_point;
            fileStream.Seek(offset, SeekOrigin.Begin);

            line += get_cnt_rem_bnd(fileStream) + ":";
            fileStream.Seek(offset, SeekOrigin.Begin);
            line += search_ff00_ffda_rst(fileStream) + ":";
            line += sos_cnt;
            Console.WriteLine(line);
            fs_result.WriteLine(line);

            fileStream.Close();
        }

        static int get_cnt_rem_bnd(FileStream fileStream)
        {
            //chunk_size=8,16,32 KB at most
            int cnt_rem_bnd = 8;
            //fileStream.Position = position;
            int[] cand = { 1, 1, 1, 1, 1, 1, 1, 1 };
            uint bound_value = (uint)((((fileStream.ReadByte() << 8) + fileStream.ReadByte()) << 8) + fileStream.ReadByte());
            uint test_value;
            for (int i = 0; i < chunk_size; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    test_value = (bound_value << (j + 8)) >> 16;
                    if ((65280 < test_value && test_value < 65488) || (65497 < test_value && test_value <= 65535))// FF01<=test_value<=FFCF or FFDA<=test_value<=FFFF
                    {
                        cand[j] = 0;
                        if (cand.Sum() == 0)
                        {
                            cnt_rem_bnd = 0;
                            return cnt_rem_bnd;
                        }
                    }
                }
                bound_value = (uint)(((bound_value << 16) >> 8) + fileStream.ReadByte());
            }

            cnt_rem_bnd = cand.Sum();
            return cnt_rem_bnd;
        }

        static string search_ff00_ffda_rst(FileStream fileStream)
        {

            // cnt_ff00:cnt_ffda:rst_in_8_loop

            //chunk_size=4 KB at most
            int cnt_ff00 = 0;// 1 - ff00
            int cnt_ffda = 0;// 2 - ffda
            int has_rst_marker = 0;// 3- rst marker flag
            int is_rst_in_mod8 = 0;// 4- rst modulus==8 flag

            bool check_rst = true;
            bool flag_loop_all_rst_markers = true;
            int next_rst_marker = 0;
            int rst_marker_offset = 0;

            //fileStream.Position = position;
            uint bound_value = readNexNBytes(fileStream, 3);
            uint test_value;
            for (int i = 0; i < chunk_size; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    test_value = (bound_value << (j + 8)) >> 16;
                    if (65280==test_value)//test_value=FF00
                    {
                        cnt_ff00++;
                    }else if (65498 == test_value)//test_value=FFDA
                    {
                        cnt_ffda++;
                    }

                    if (check_rst)
                    {
                        if (flag_loop_all_rst_markers)
                        {
                            for (int k=0;k<8;k++)
                            {
                                if ((65488+k) == test_value)//test_value=FF00
                                {
                                    flag_loop_all_rst_markers = false;
                                    next_rst_marker = (k < 7) ? 65488 + k + 1 : 65488;
                                    rst_marker_offset = j;
                                    has_rst_marker = 1;
                                    is_rst_in_mod8 = 1;
                                }
                            }
                        }
                        else
                        {
                            if (next_rst_marker == test_value)
                            {
                                if (rst_marker_offset==j)
                                {
                                    // continue
                                    next_rst_marker = (next_rst_marker == 65495) ? 65488 : next_rst_marker+1;
                                }else
                                {
                                    // rst marker modulus is not 8; break not a jpeg encoded data (for RST marker included ones)
                                    check_rst = false;
                                    is_rst_in_mod8 = 0;
                                }
                            }
                        }
                    }
                    

                }
                bound_value = (uint)(((bound_value << 16) >> 8) + fileStream.ReadByte());
            }
            return cnt_ff00 + ":" + cnt_ffda + ":" + has_rst_marker + ":" + is_rst_in_mod8;
        }

        static string get_cnt_sos(FileStream fileStream)
        {
            //0xFFDA00 08010100 003F00
            //0xFFDA00 0C030100 02110311 003F00
            //0xFFDA00 0C030000 01110211 003F00
            //0xFFDA00 0C030000 01000200 003F00
            //0xFFDA00 0C035200 47004200 003F00

            string[] sos2nd = { "0x08010100", "0x0C030100", "0x0C030000", "0x0C035200"};
            string[] sos3rd = { "0x02110311", "0x01110211", "0x01000200", "0x47004200" };
            int[] cnt_sos = new int[5];// keeps number of sos markers in above order
            uint bit_buffer = readNexNBytes(fileStream, 4);
            int offset;
            uint remainder = 0;
            uint sos_buffer = 0;

            int sos_i, sos_fin;
            for (int i = 0; i < chunk_size; i++)
            {
                offset = searchHex(bit_buffer,"0xFFDA00");
                remainder = getRemainder(bit_buffer, offset);
                if (offset < 8)
                {
                    // hit
                    bit_buffer = readNexNBytes(fileStream, 4);
                    sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                    remainder = getRemainder(bit_buffer, offset);

                    // search 2nd sos blocks
                    sos_i = searchSosBlock(sos_buffer, sos2nd);

                    if (sos_i < 4)
                    {
                        //hit
                        if (sos_i > 0)
                        {
                            //check for 3rd blocks in sos-2/3/4/5
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_i = searchSosBlock(sos_buffer, sos3rd)+1;
                        }

                        if (sos_i<5)
                        {
                            // check sos_fin
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_fin = searchHex(sos_buffer, "0x003F00");
                            if (sos_fin < 8)
                            {
                                //match!
                                cnt_sos[sos_i] += 1;
                            }
                        }
                    }
                }
                bit_buffer = (uint)(( (bit_buffer << 8) + fileStream.ReadByte()));
            }

            return cnt_sos[0] + ":" + cnt_sos[1] + ":" + cnt_sos[2] + ":" + cnt_sos[3] + ":" + cnt_sos[4];
        }

        static Tuple<string, long> get_sos_cnt_and_point(FileStream fileStream)
        {
            //0xFFDA00 08010100 003F00
            //0xFFDA00 0C030100 02110311 003F00
            //0xFFDA00 0C030000 01110211 003F00
            //0xFFDA00 0C030000 01000200 003F00
            //0xFFDA00 0C035200 47004200 003F00

            string[] sos2nd = { "0x08010100", "0x0C030100", "0x0C030000", "0x0C035200" };
            string[] sos3rd = { "0x02110311", "0x01110211", "0x01000200", "0x47004200" };
            int[] cnt_sos = new int[5];// keeps number of sos markers in above order
            long sos_point = 0; // holds latest sos_point
            uint bit_buffer = readNexNBytes(fileStream, 4);
            int offset;
            uint remainder = 0;
            uint sos_buffer = 0;

            int sos_i, sos_fin;

            long last_point = fileStream.Length / 4;

            while (fileStream.Position<last_point)
            {
                offset = searchHex(bit_buffer, "0xFFDA00");
                remainder = getRemainder(bit_buffer, offset);
                if (offset < 8)
                {
                    // hit
                    bit_buffer = readNexNBytes(fileStream, 4);
                    sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                    remainder = getRemainder(bit_buffer, offset);

                    // search 2nd sos blocks
                    sos_i = searchSosBlock(sos_buffer, sos2nd);

                    if (sos_i < 4)
                    {
                        //hit
                        if (sos_i > 0)
                        {
                            //check for 3rd blocks in sos-2/3/4/5
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_i = searchSosBlock(sos_buffer, sos3rd) + 1;
                        }

                        if (sos_i < 5)
                        {
                            // check sos_fin
                            bit_buffer = readNexNBytes(fileStream, 4);
                            sos_buffer = join2Remainder(remainder, bit_buffer, offset);
                            remainder = getRemainder(bit_buffer, offset);
                            sos_fin = searchHex(sos_buffer, "0x003F00");
                            if (sos_fin < 8)
                            {
                                //match!
                                cnt_sos[sos_i] += 1;
                                sos_point = fileStream.Position+((sos_i==0)?11:15);
                            }
                        }
                    }
                }
                bit_buffer = (uint)(((bit_buffer << 8) + fileStream.ReadByte()));
            }

            return Tuple.Create(cnt_sos[0] + ":" + cnt_sos[1] + ":" + cnt_sos[2] + ":" + cnt_sos[3] + ":" + cnt_sos[4], sos_point);
        }

        static uint readNexNBytes(FileStream fileStream,int n)
        {
            // reads maximum next n bytes
            uint nextNBytes = 0;
            for(int i = 0; i < n; i++)
            {
                nextNBytes = (uint)( (nextNBytes<<8)+fileStream.ReadByte());
            }

            return nextNBytes;
        }

        static int searchHex(uint bit_buffer, string prefixedHex)
        {
            uint test_value;
            int j;
            for (j = 0; j < 8; j++)
            {
                test_value = (bit_buffer << j) >> 8;
                if (hex2int(prefixedHex) == test_value)//test_value=prefixedHex
                {
                    return j;
                }
            }
            return j;
        }
        static int hex2int(string prefixedHex)
        {
            return Convert.ToInt32(prefixedHex, 16);
        }
        static uint getRemainder(uint bit_buffer,int j)
        {
            // get remainder bits shifted to lhs
            return (bit_buffer << (j + 24));
        }

        static uint join2Remainder(uint remainder, uint bit_buffer, int j)
        {
            return remainder + ((bit_buffer << j) >> (8-j));
        }

        static int searchSosBlock(uint sos_buffer, string[] sos_block)
        {
            int k;
            for (k = 0; k < sos_block.Length; k++)
            {
                if (sos_buffer == (uint)hex2int(sos_block[k]))
                {
                    break;
                }
            }

            return k;
        }
    }
}
