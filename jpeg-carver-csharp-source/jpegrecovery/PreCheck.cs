using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace JpegRecovery
{
    class PreCheck
    {
        int chunk_size = 4 * 1024;
        public bool isJpeg(FileStream fileStream)
        {
            //1)If the number of remaining byte boundaries is equal tozero, it is accepted as non - JPEG encoded data.
            //2)If the number of remaining byte boundaries is equal toexactly one, it is accepted as JPEG encoded data.
            //3)If the  number of  remaining  byte boundaries  is biggerthan  one,  then,  check through  the Oscar  method toaccept or reject the encoded data
            long cpoint = fileStream.Position;
            int cnt_rem_bnd = get_cnt_rem_bnd(fileStream);
            //int cnt_rem_bnd = 2;// only run oscar method

            if (cnt_rem_bnd == 0)
            {
                // non-jpeg
                return false;
            }
            else if (cnt_rem_bnd == 1)
            {
                //jpeg
                return true;
            }
            else
            {
                // follow oscar method
                fileStream.Position = cpoint;
                int[] ff00_ffda_brst_crst = search_ff00_ffda_rst(fileStream);
                bool flag_ff00 = (ff00_ffda_brst_crst[0] > 9.7) && (ff00_ffda_brst_crst[0] < 47);
                bool flag_rst = (ff00_ffda_brst_crst[2] == 0) || (ff00_ffda_brst_crst[3] == 1);

                //flag_rst
                //a--> ff00_ffda_brst_crst[2] --> has rst marker
                //b--> ff00_ffda_brst_crst[3] --> is in mod 8
                //-----------------------------------------------
                //a   b|flag_rst
                //0   0|1
                //0   1|1
                //1   0|0
                //1   1|1
                //flag_rst=( not a) or b

                return flag_ff00 && flag_rst;
            }
        }

        int get_cnt_rem_bnd(FileStream fileStream)
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

        int[] search_ff00_ffda_rst(FileStream fileStream)
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
                    if (65280 == test_value)//test_value=FF00
                    {
                        cnt_ff00++;
                    }
                    else if (65498 == test_value)//test_value=FFDA
                    {
                        cnt_ffda++;
                    }

                    if (check_rst)
                    {
                        if (flag_loop_all_rst_markers)
                        {
                            for (int k = 0; k < 8; k++)
                            {
                                if ((65488 + k) == test_value)//test_value=FF00
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
                                if (rst_marker_offset == j)
                                {
                                    // continue
                                    next_rst_marker = (next_rst_marker == 65495) ? 65488 : next_rst_marker + 1;
                                }
                                else
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
            //return cnt_ff00 + ":" + cnt_ffda + ":" + has_rst_marker + ":" + is_rst_in_mod8;
            return new int[] { cnt_ff00, cnt_ffda, has_rst_marker, is_rst_in_mod8 };
        }


        public Tuple<int, long> get_sos_cnt_and_point(FileStream fileStream, bool isChunk=false)
        {
            //0xFFDA00 08010100 003F00
            //0xFFDA00 0C030100 02110311 003F00
            //0xFFDA00 0C030000 01110211 003F00
            //0xFFDA00 0C030000 01000200 003F00
            //0xFFDA00 0C035200 47004200 003F00
            long last_point = (isChunk) ? fileStream.Position + 4 * 1024 : fileStream.Length;

            string[] sos2nd = { "0x08010100", "0x0C030100", "0x0C030000", "0x0C035200" };
            string[] sos3rd = { "0x02110311", "0x01110211", "0x01000200", "0x47004200" };
            int[] cnt_sos = new int[5];// keeps number of sos markers in above order
            long sos_point = 0; // holds latest sos_point
            uint bit_buffer = readNexNBytes(fileStream, 4);
            int offset;
            uint remainder = 0;
            uint sos_buffer = 0;

            int sos_i=-1, sos_fin;

            

            while (fileStream.Position < last_point)
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
                                sos_point = fileStream.Position + ((sos_i == 0) ? 11 : 15);
                                break;
                            }
                        }
                    }
                }
                bit_buffer = (uint)(((bit_buffer << 8) + fileStream.ReadByte()));
            }
            // if there is no hit returns (-1,0)
            return Tuple.Create(sos_i, sos_point);
        }

        uint readNexNBytes(FileStream fileStream, int n)
        {
            // reads maximum next n bytes
            uint nextNBytes = 0;
            for (int i = 0; i < n; i++)
            {
                nextNBytes = (uint)((nextNBytes << 8) + fileStream.ReadByte());
            }

            return nextNBytes;
        }

        int searchHex(uint bit_buffer, string prefixedHex)
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

        int hex2int(string prefixedHex)
        {
            return Convert.ToInt32(prefixedHex, 16);
        }

        uint getRemainder(uint bit_buffer, int j)
        {
            // get remainder bits shifted to lhs
            return (bit_buffer << (j + 24));
        }

        uint join2Remainder(uint remainder, uint bit_buffer, int j)
        {
            return remainder + ((bit_buffer << j) >> (8 - j));
        }

        int searchSosBlock(uint sos_buffer, string[] sos_block)
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
