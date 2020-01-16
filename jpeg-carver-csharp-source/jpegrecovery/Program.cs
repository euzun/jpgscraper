using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using System.Diagnostics;

namespace JpegRecovery
{
    class Program
    {
        
        FileStream fileStream;
        String outName;
        int cnt;//next bit counter in byte buffer
        int byte_boundary;
        int is_not_rst_marker;// the flag for checking whether a restart marker come or not.
        
        ushort maxshort = ushort.MaxValue;
        public long initPosition = 0;
        public long endOfStream;
        long tempLast = 0;
        long[] lastPoint=new long[3];


        byte _byte; //byte buffer
        byte byte1; //start by loading first byte of the stream. used with byte2 to calculate nextbyte
        byte byte2 = 0;

        bool is_terminated;//(is_invalid_cw||is_end_of_stream)
        bool is_end_of_stream;// end of stream. default false, return true if eos  is reached and process is terminated
        bool is_invalid_cw;// error condition 1. return true if an invalid codeword is faced during decoding and process is terminated
        bool is_coeff_overflow;// error condition 2. return true if coefficient matrix overload 64 coeefficients during decoding, but not terminated now. it will be used for file end detection

        int overload = 0, invalidcw = 0;
        int[] invalid_codeword = { 0, 0, 0 };// invalid_codeword flag array for YCbCr, YYCbCr and YYYYCbCr mcu decodings, respectively. return 0 for the index in which an invalid codeword is faced.
        int[] overflow_codeword = { 0, 0, 0 };

        string filePar;
        Chroma ch;
        Dimension d;
        int mcu, width;
        
        public Program(string file)
        {
            filePar = file;
            fileStream = new FileStream(file, FileMode.Open);
            endOfStream = fileStream.Length;
            findByteBoundary();
            initPosition = 0;
            outName = file + ".jpg";
        }

        public Program(string file, FileStream _fileStream, long _initPosition)
        {
            fileStream = _fileStream;
            initPosition = _initPosition;

            endOfStream = fileStream.Length;
            fileStream.Position = initPosition;

            findByteBoundary();
            outName = file + ".jpg";
        }

        public Program(string file, FileStream _fileStream, long _initPosition, long _lastPosition)
        {
            fileStream = _fileStream;
            initPosition = _initPosition;
            endOfStream = _lastPosition+1024;

            fileStream.Position = initPosition;

            findByteBoundary();
            outName = file + ".jpg";
        }

        public long finalizeDecoding()
        {
            try
            {
                Offset o = new Offset();
                int offset = o.imageOffset(Global.rgbList, width, Global.chr);
                RGBBuilder.writePixels(Global.rgbList, width, Global.chr, offset, outName);

                Console.WriteLine("Fragment Saved: " + outName);
                Console.WriteLine("-------------------------------------------------------");
                Console.WriteLine();
            }
            catch(Exception e) { Console.WriteLine("Offset--> "+e.Message); }
            
            return lastPoint[mcu];
        }

        public bool startDecoding()
        {
            List<int[][]>[] blockListWith1Y = decodeAs(1);
            lastPoint[0] = tempLast;
            List<int[][]>[] blockListWith2Y = decodeAs(2);
            lastPoint[1] = tempLast;
            List<int[][]>[] blockListWith4Y = decodeAs(4);
            lastPoint[2] = tempLast;

            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

            //fileStream.Close();
            ch = new Chroma(blockListWith1Y, blockListWith2Y, blockListWith4Y, invalid_codeword,overflow_codeword);
            mcu=ch.chromaSubsampling();
            fileStream.Position = lastPoint[mcu];

            d = new Dimension();
            width=d.imageWidth();

            //stopwatch.Stop();
            //Console.WriteLine("HeaderTime: "+ stopwatch.ElapsedMilliseconds);

            return (d.wlist.Distinct().Count() == 5) ? false : true;
            
        }
        
        void findByteBoundary()
        {
            // to be implemented
            int[] cand = { 1, 1, 1, 1, 1, 1, 1, 1 };
            uint bound_value = (uint)((((fileStream.ReadByte() << 8) + fileStream.ReadByte()) << 8) + fileStream.ReadByte());
            uint test_value;
            for (int i = 0; i < 8 * 1024; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    test_value = (bound_value << (j + 8)) >> 16;
                    if ((65280 < test_value && test_value < 65488) || (65497 < test_value && test_value <= 65535))// FF01<=test_value<=FFCF or FFDA<=test_value<=FFFF
                    {
                        cand[j] = 0;
                        if (cand.Sum() == 1)
                        {
                            byte_boundary = Array.IndexOf(cand, 1);
                            fileStream.Position = initPosition;
                            return;
                        }
                    }
                }
                bound_value = (uint)(((bound_value << 16) >> 8) + fileStream.ReadByte());
            }
            fileStream.Position = initPosition;
        }

        void initializeSettings()
        {
            cnt = 0;
            is_not_rst_marker = 1;

            fileStream.Position = initPosition;

            _byte = 0; //byte buffer
            byte1 = (byte)fileStream.ReadByte();
            byte2 = 0;
            
            overload = 0;
            invalidcw = 0;

            is_terminated = false;
            is_end_of_stream = false;
            is_invalid_cw = false;
            is_coeff_overflow = false;

            
            
        }
        
        byte nextBit()
        {
            if (cnt == 0)
            {
                _byte = nextByte();
                cnt = 8;

                if (_byte == 255)// FF
                {
                    byte b2 = nextByte();

                    if (208 <= b2 && b2 <= 215)//D0 to D7
                    {
                        // b and b2 built a restart marker
                        // 1- reset dc to the next readed value, that is, do not take difference
                        is_not_rst_marker = 0;
                        _byte = nextByte();
                    }
                    //else if (b2 == 0)
                    //{
                    //    // b2 is the stuffed byte
                    //    // continue
                    //}
                }
            }

            byte _bit = (byte)(_byte >> 7); // get next bit by 7 left logical shift of byte buffer
            cnt--;
            _byte = (byte)(_byte << 1);

            return _bit;
        }

        byte nextByte()
        {
            byte my_byte=0;

            if (fileStream.Position < endOfStream)
            {
                byte2 = (byte)fileStream.ReadByte();
                my_byte = (byte)((byte1 << byte_boundary) + (byte2 >> (8 - byte_boundary)));
                byte1 = byte2;
                tempLast = fileStream.Position;
            }
            else
            {
                is_end_of_stream = true;
                is_terminated = true;
                tempLast = endOfStream;
            }

            return my_byte;
        }

        byte decode(int compIndex)
        {
            byte value = 255;
            try
            {
                UInt16 code = nextBit();
                for (int i = 0; (i < Huffman.maxcodelength[compIndex]) && (!is_end_of_stream); i++)
                {
                    if (code > Huffman.maxcode[compIndex][i])
                    {
                        code = (UInt16)((code << 1) + nextBit());
                        if (code == maxshort >> (16 - Huffman.maxcodelength[compIndex]))
                        {// invalid codeword
                            invalidcw++;
                            break;
                        }
                    }
                    else
                    {
                        int j = Huffman.valptr[compIndex][i];
                        j = j + code - Huffman.mincode[compIndex][i];
                        value = Huffman.huffval[compIndex][j];
                        break;
                    }
                }
                is_invalid_cw = (value == 255);// true if value equals 255
            }catch(Exception e){
                is_invalid_cw = true;// true if value equals 255
            }
            is_terminated = is_invalid_cw || is_end_of_stream;

            return value;
        }

        int receive(int s)
        {
            //TISO1490-93/d087
            int i = 0;
            int v = 0;
            while (i != s && !is_terminated)
            {
                v = (v << 1) + nextBit();
                i++;
            }
            return v;
        }

        int extend(int v,int t)
        {
            // TISO1440-93/d082
            return (v < Math.Pow(2, t - 1)) ? (v + ((-1 << t) + 1)) : v;
        }

        int decodeDC(int dcIndex, int preDC)
        {
            byte t = decode(dcIndex);
            if (is_terminated) { return 0; }// invalid codeword or end of stream
            int diff = extend(receive(t), t) + is_not_rst_marker * preDC;
            is_not_rst_marker = 1;
            return diff;

        }

        int[][] decodeAC(int acIndex, int[][] block)
        {
            //TISO1450-93/d083
            int k = 0, s = 0, r = 0;
            byte rs;

            while (!is_terminated)
            {
                rs = decode(acIndex);

                if (is_terminated) { break; } // invalid codeword or end of stream
                
                s = rs % 16;
                r = rs >> 4;

                if (s == 0)
                {
                    if (r == 0) { break; } //00 EOB codeword
                    k += 16;//F0 ZRL codeword
                }
                else
                {
                    k += r;
                    is_coeff_overflow = (k > 62);
                    overload += (is_coeff_overflow) ? 1 : 0;
                    if (overload > 10)
                    {
                        is_terminated = true;
                        is_end_of_stream = true;
                        break;
                    }

                    if (is_coeff_overflow) { break; }
                    block[Global.zzmask[k + 1] % 8][Global.zzmask[k + 1] / 8] = extend(receive(s), s);
                    if (k == 62) { break; }
                    k++;
                }
            }
            return block;
        }

        int[][] decodeBlock(int dcIndex, int acIndex, int preDC)
        {
            int[][] block = new int[8][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
            block[0][0] = decodeDC(dcIndex, preDC);
            return (is_terminated) ? block : decodeAC(acIndex, block);//exit if invalid codeword is reached in decodeDC, else, decodeAC
        }

        List<int[][]>[] decodeAs(int y_count)
        {
            //long fpoint = fileStream.Position;
            //var stopwatch = new Stopwatch();
            //stopwatch.Start();




            initializeSettings();

            int y_pre_dc = 0, cb_pre_dc = 0, cr_pre_dc = 0;
            List<int[][]>[] blockList = new List<int[][]>[3] { new List<int[][]>(), new List<int[][]>(), new List<int[][]>() };
            int[][][] y_block = new int[y_count][][];
            int[][] cb_block, cr_block;

            //while (!is_terminated)
            while (!is_end_of_stream)
            {
                //for (int i = 0; (i < y_count) && !is_terminated; i++)
                for (int i = 0; (i < y_count) && !is_end_of_stream; i++)
                {
                    y_block[i] = decodeBlock(0, 1, y_pre_dc);
                    y_pre_dc = y_block[i][0][0];
                }

                //if (is_terminated) { break; } // invalid codeword or end of stream
                if (is_end_of_stream) { break; } // invalid codeword or end of stream
                cb_block = decodeBlock(2, 3, cb_pre_dc);
                cb_pre_dc = cb_block[0][0];

                //if (is_terminated) { break; } // invalid codeword or end of stream
                if (is_end_of_stream) { break; } // invalid codeword or end of stream
                cr_block = decodeBlock(2, 3, cr_pre_dc);
                cr_pre_dc = cr_block[0][0];

                //if (is_terminated) { break; } // invalid codeword or end of stream
                if (is_end_of_stream) { break; } // invalid codeword or end of stream

                for (int i = 0; i < y_count; i++)
                {
                    blockList[0].Add(y_block[i]);
                }
                blockList[1].Add(cb_block);
                blockList[2].Add(cr_block);
            }

            //valid_codeword[y_count / 2] =((!is_end_of_stream)&&is_invalid_cw)? 0:1; // y_count=1-->0, y_count=2-->1,y_count=4-->2
            invalid_codeword[y_count / 2] = Math.Sign(invalidcw); // y_count=1-->0, y_count=2-->1,y_count=4-->2
            overflow_codeword[y_count / 2] = (overload >= 10) ? 1 : 0;


            //stopwatch.Stop();
            //long elapsed_time = stopwatch.ElapsedMilliseconds;
            //long lpoint = fileStream.Position;
            //Console.WriteLine("stream: "+(lpoint-fpoint) + "time: "+elapsed_time);
            
                
            return blockList;
        }

    }
}
