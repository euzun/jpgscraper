using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace JpegRecovery
{
    class MainClass
    {

        PreCheck preCheck = new PreCheck();
        int[] nrof_par_head = new int[25];
        long[] len_par_head = new long[25];

        int[] nrof_cand_non_head = new int[25];
        long[] len_cand_non_head = new long[25];
        int[]  nrof_rec_non_head = new int[25];
        long[] len_rec_non_head = new long[25];

        int current_sd = 0;

        [STAThread]
        static void Main(string[] args)
        {
            MainClass m = new MainClass();
            Huffman.switchHuffman(1);


            /*
             *Procedur 1- Recover single jpeg encoded data.
             * 
             */
            //Program p;
            //OpenFileDialog openFileDialog1 = new OpenFileDialog();
            //openFileDialog1.InitialDirectory = @"E:\";
            //DialogResult result = openFileDialog1.ShowDialog();
            //if (result == DialogResult.OK) // Test result.
            //{
            //    int counter = m.initializeOrphanedFragments(openFileDialog1.FileName);
            //    m.recoverOrphanedFragments(counter);
            //    //Stopwatch watch = Stopwatch.StartNew();
            //    //p = new Program(openFileDialog1.FileName);
            //    //p.startDecoding();
            //    //watch.Stop();
            //    //Console.WriteLine(watch.Elapsed);
            //}



            ///* 
            // * Procedur 2- Recover whole storage volume.
            // * 
            // */

            //for (int i = 1; i < 26; i++)
            //{

            //    String basePath = Global.recOrphanDir + i + "\\img_";
            //    int counter = m.initializeOrphanedFragments(Global.recOrphanDir + i + ".raw", basePath);
            //    m.recoverOrphanedFragments(counter, basePath);


            //    Console.WriteLine(i+" Done!!");
            //}


            //// partial header
            //for (int i = 4; i < 26; i++)
            //{
            //    m.process_sd_par_head(i);
            //}

            //for (int i = 1; i < 2; i++)
            //{
            //    m.process_sd_non_head(i);
            //    m.print_stats();
            //}

            //m.process_sd_non_head(2);
            //m.print_stats();


            DateTime dt = DateTime.Now;
            //m.process_sd_par_head(1);
            m.process_sd_non_head(1);
            TimeSpan ts = DateTime.Now - dt;
            m.print_stats();

            Console.WriteLine("time: "+ts.TotalMilliseconds.ToString());
            Console.ReadLine();

        }
        
        void process_sd_par_head(int i)
        {
            current_sd = i-1;
            Directory.CreateDirectory(Global.cand_non_head_dir + i);
            Directory.CreateDirectory(Global.rec_par_head_dir + i);
            String candRecPath = Global.cand_rec_dir + i + ".raw";
            String candNonHeadPath = Global.cand_non_head_dir + i + ".raw";
            String parHeadPre = Global.rec_par_head_dir + i + "\\par_head_";

            recover_par_head(candRecPath, candNonHeadPath, parHeadPre);

            Console.WriteLine("Partial Headers Recovered at sd-: "+i);
        }
        // recovers jpegs with partial header and residual segments concatenated
        // and saved under can_non_head directory as single file (e.g., non_head_1.raw)
        void recover_par_head(string candRecPath, string candNonHeadPath, string parHeadPre)
        {

            List<long[]> par_points = new List<long[]>();

            FileStream candRecStream = new FileStream(candRecPath, FileMode.Open);
            var sos = preCheck.get_sos_cnt_and_point(candRecStream);
            int sos_index = sos.Item1; // which SOS code is hit
            long sos_point = sos.Item2; // point of encoded data starts. go ahead and recover consequent jpeg

            int par_cnt = 0;
            long par_last_position = 0;

            while (sos_point>0 && candRecStream.Position < candRecStream.Length - 1024)
            {
                //Console.WriteLine("sos_point: " + sos_point + " , sos_index: " + sos_index);
                string par_head_i = parHeadPre + par_cnt;
                par_cnt++;
                par_last_position = recover_par_segment(par_head_i, candRecStream, sos_point);// recovered segment length

                par_points.Add(new long[] { sos_point, par_last_position });
                Console.WriteLine(Math.Round(sos_point/1024.0)+" -- " + Math.Round(par_last_position/1024.0));

                sos = preCheck.get_sos_cnt_and_point(candRecStream);
                sos_index = sos.Item1;
                sos_point = sos.Item2;
            }

            nrof_par_head[current_sd] = par_points.Count;
            split_par_and_non_head(parHeadPre, candNonHeadPath, candRecStream, par_points);
            candRecStream.Close();
        }
        long recover_par_segment(string par_head_i, FileStream candRecStream, long sos_point)
        {
            long lastPosition = 0;

            long maxLastPosition = 0;
            int maxHuffmanIndex = 1;
            Program p;
            for (int i = 1; i < 2; i++)
            {
                try
                {
                    Huffman.switchHuffman(i);
                    p = new Program(par_head_i + "_" + i, candRecStream, sos_point);
                    p.startDecoding();
                    //if (p.startDecoding())
                    //{
                    lastPosition = p.finalizeDecoding();
                    if (maxLastPosition < lastPosition)
                    {
                        maxLastPosition = lastPosition;
                        maxHuffmanIndex = i;
                    }
                    //}
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
            }

            try
            {
                if (maxHuffmanIndex < 2)
                {
                    Huffman.switchHuffman(maxHuffmanIndex);
                    p = new Program(par_head_i + "_" + maxHuffmanIndex, candRecStream, sos_point);
                    p.startDecoding();
                    return p.finalizeDecoding();
                }


            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            lastPosition = candRecStream.Position;
            return lastPosition;
        }
        void save_par_segment_binary(string par_head_i, FileStream candRecStream, long fpoint, long lpoint)
        {
            FileStream par_segment_stream = new FileStream(par_head_i, FileMode.Create);

            //byte[] buffer = new byte[lpoint - fpoint];
            //candRecStream.Read(buffer, 0, buffer.Length);
            //par_segment_stream.Write(buffer, 0, buffer.Length);
            for (long i = fpoint; i < lpoint; i++)
            {
                par_segment_stream.WriteByte((byte)candRecStream.ReadByte());
            }

            par_segment_stream.Close();
        }
        void dump_cand_non_head(FileStream candNonHeadStream, FileStream candRecStream, long fpoint, long lpoint)
        {
            //byte[] buffer = new byte[lpoint - fpoint];
            //candRecStream.Read(buffer, 0, buffer.Length);
            //candNonHeadStream.Write(buffer, 0, buffer.Length);

            for(long i = fpoint; i < lpoint; i++)
            {
                candNonHeadStream.WriteByte((byte)candRecStream.ReadByte());
            }
        }
        void split_par_and_non_head(string parHeadPre, string candNonHeadPath, FileStream candRecStream, List<long[]> par_points)
        {
            long par_head_len=0, non_head_len=0;
            candRecStream.Position = 0;
            long non_head_fpoint = 0;
            long non_head_lpoint = 0;

            long par_fpoint = 0;
            long par_lpoint = 0;

            FileStream candNonHeadStream = new FileStream(candNonHeadPath, FileMode.Create);
            
            for (int i = 0; i < par_points.Count; i++)
            {
                par_fpoint = par_points.ElementAt(i)[0];
                par_lpoint = par_points.ElementAt(i)[1];
                par_head_len += (par_lpoint - par_fpoint);

                non_head_lpoint = par_fpoint;
                non_head_len += (non_head_lpoint - non_head_fpoint);

                dump_cand_non_head(candNonHeadStream, candRecStream, non_head_fpoint, non_head_lpoint);
                save_par_segment_binary(parHeadPre + i, candRecStream, par_fpoint, par_lpoint);
                non_head_fpoint = par_lpoint;
            }
            non_head_lpoint = candRecStream.Length;
            non_head_len += (non_head_lpoint - non_head_fpoint);
            dump_cand_non_head(candNonHeadStream, candRecStream, non_head_fpoint, non_head_lpoint);

            candNonHeadStream.Close();
            len_par_head[current_sd] = par_head_len;
            len_cand_non_head[current_sd] = non_head_len;
        }

        void print_stats()
        {
            //for(int i = 0; i < nrof_par_head.Length; i++)
            //{
            //    Console.WriteLine(String.Format("{0,12:F1}{1,12:F2}{2,12:F2}",   nrof_par_head[i], Convert.ToDouble(len_par_head[i]) / 1024.0 , Convert.ToDouble(len_cand_non_head[i]) / 1048576.0));
            //}

            for (int i = 0; i < nrof_rec_non_head.Length; i++)
            {
                Console.WriteLine(String.Format("{0,12:F1}{1,12:F2}{2,12:F1}{3,12:F2}", nrof_cand_non_head[i], Convert.ToDouble(len_cand_non_head[i]) / 1048576.0, nrof_rec_non_head[i], Convert.ToDouble(len_rec_non_head[i]) / 1048576.0));
            }
        }


        void process_sd_non_head(int i)
        {
            current_sd = i - 1;
            Directory.CreateDirectory(Global.rec_non_head_dir + i);
            string candNonHeadPath = Global.cand_non_head_dir + i + ".raw";
            string nonHeadPre = Global.rec_non_head_dir + i + "\\non_head_";

            DateTime dt = DateTime.Now;
            List<long[]> jpg_data_points = find_cand_non_head_fragments(candNonHeadPath);
            TimeSpan ts = DateTime.Now - dt;

            Console.WriteLine("time: " + ts.TotalSeconds.ToString());
           
            recover_non_head_fragments(candNonHeadPath, nonHeadPre, jpg_data_points);

            Console.WriteLine("Partial Headers Recovered at sd-: " + i);

        }
        List<long[]> find_cand_non_head_fragments(string candNonHeadPath)
        {
            List<long[]> jpg_data_points = new List<long[]>();

            long fpoint = 0, lpoint = 0;
            bool isJpeg=false, pair_save=false;

            FileStream cand_non_head_stream = new FileStream(candNonHeadPath, FileMode.Open);
            long endOfStream = cand_non_head_stream.Length;

            while(cand_non_head_stream.Position <= endOfStream - 4096)
            {
                fpoint = cand_non_head_stream.Position;
                isJpeg = preCheck.isJpeg(cand_non_head_stream);
                while (isJpeg && (cand_non_head_stream.Position < endOfStream - 1024))
                {
                    pair_save = true;
                    lpoint = cand_non_head_stream.Position;
                    isJpeg = preCheck.isJpeg(cand_non_head_stream);
                }
                if (pair_save)
                {
                    pair_save = false;
                    len_cand_non_head[current_sd] += (lpoint - fpoint);
                    jpg_data_points.Add(new long[] { fpoint, lpoint });
                    cand_non_head_stream.Position = lpoint + 4096;
                }
                else
                {
                    cand_non_head_stream.Position = fpoint + 4096;
                }
            }

            nrof_cand_non_head[current_sd] = jpg_data_points.Count;
            cand_non_head_stream.Close();

            //FileStream cand_jpg_stream;
            //long fPos = 0, lPos = 0;
            //int counter = 0;
            //bool isJpeg;
            //long endOfStream = fileStream.Length;
            //bool newFile = false;

            //while (fileStream.Position < endOfStream)
            //{
            //    fPos = fileStream.Position;
            //    isJpeg = pc.isJpeg(fileStream);
            //    while (isJpeg)
            //    {
            //        newFile = true;
            //        lPos = fileStream.Position;
            //        isJpeg = pc.isJpeg(fileStream);
            //    }
            //    if (newFile)
            //    {
            //        byte[] buffer = new byte[lPos - fPos];
            //        fsw = new FileStream(basePath + counter + "_0", FileMode.Create);
            //        fileStream.Position = fPos;
            //        fileStream.Read(buffer, 0, (int)(lPos - fPos));
            //        fileStream.Position = lPos + 8 * 1024;
            //        fsw.Write(buffer, 0, buffer.Length);
            //        fsw.Close();
            //        counter++;
            //        newFile = false;
            //    }
            //}
            //fileStream.Close();
            return jpg_data_points;
        }

        void recover_non_head_fragments(string candNonHeadPath, string nonHeadPre, List<long[]> jpg_data_points)
        {
            FileStream cand_non_head_stream = new FileStream(candNonHeadPath, FileMode.Open);

            List<long[]> rec_jpg_segments = new List<long[]>();
            long fpoint, lpoint, segment_size, rec_point;
            int cnt = 0;

            for (int i=0;i< jpg_data_points.Count; i++)
            {
                fpoint = jpg_data_points.ElementAt(i)[0];
                lpoint = jpg_data_points.ElementAt(i)[1];
                segment_size = lpoint - fpoint;

                rec_point = recover_non_head_segment(nonHeadPre + cnt, cand_non_head_stream, fpoint, lpoint);
                if (rec_point > 0)
                {
                    // a jpeg fragment is saved. So save the fragment binary
                    rec_jpg_segments.Add(new long[] { fpoint, rec_point });
                    cnt++;
                }

                while (cand_non_head_stream.Position < (fpoint+segment_size / 2) )
                {
                    //if still second half of of the segment is available
                    fpoint = cand_non_head_stream.Position + 128;// start again after 128 byte
                    segment_size = lpoint - fpoint;
                    rec_point = recover_non_head_segment(nonHeadPre + cnt, cand_non_head_stream, fpoint, lpoint);
                    if (rec_point > 0)
                    {
                        // a jpeg fragment is saved. So save the fragment binary
                        rec_jpg_segments.Add(new long[] { fpoint, rec_point });
                        cnt++;
                    }
                }
            }

            nrof_rec_non_head[current_sd] = rec_jpg_segments.Count;

            for (int i=0;i< rec_jpg_segments.Count; i++)
            {
                fpoint = rec_jpg_segments.ElementAt(i)[0];
                lpoint = rec_jpg_segments.ElementAt(i)[1];
                
                save_non_head_segment_binary(nonHeadPre + i, cand_non_head_stream, fpoint,lpoint);
                len_rec_non_head[current_sd] += (lpoint - fpoint);
            }

            cand_non_head_stream.Close();
        }

        long recover_non_head_segment(string non_head_i, FileStream cand_non_head_stream, long fpoint, long lpoint)
        {
            long lastPosition = 0;


            long maxLastPosition = 0;
            int maxHuffmanIndex = 1;
            Program p;

            for (int i = 1; i < 2; i++)
            {
                try
                {
                    Huffman.switchHuffman(i);
                    p = new Program(non_head_i + "_" + i, cand_non_head_stream, fpoint, lpoint);
                    p.startDecoding();
                    //if (p.startDecoding())
                    //{
                    lastPosition = p.finalizeDecoding();
                    if (maxLastPosition < lastPosition)
                    {
                        maxLastPosition = lastPosition;
                        maxHuffmanIndex = i;
                    }
                    //}
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
            }
            try
            {
                if (maxHuffmanIndex < 2)
                {
                    Huffman.switchHuffman(maxHuffmanIndex);
                    p = new Program(non_head_i + "_" + maxHuffmanIndex, cand_non_head_stream, fpoint, lpoint);
                    p.startDecoding();
                    return p.finalizeDecoding();
                }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
            return lastPosition;
        }

        void save_non_head_segment_binary(string non_head_i, FileStream cand_non_head_stream, long fpoint, long lpoint)
        {
            cand_non_head_stream.Position = fpoint;

            FileStream non_head_segment_stream = new FileStream(non_head_i, FileMode.Create);
            for (long i = fpoint; i < lpoint; i++)
            {
                non_head_segment_stream.WriteByte((byte)cand_non_head_stream.ReadByte());
            }

            non_head_segment_stream.Close();
        }







        //void recoverOrphanedFragments(int counter, String basePath)
        //{
        //    Program p;
        //    int subCounter = 0;
        //    long recLength = 0;
        //    for (int i = 0; i < counter; i++)
        //    {
        //        try
        //        {
        //            subCounter = 0;
        //            recLength = 0;

        //            p = new Program(basePath + i + "_" + subCounter);
        //            recLength = recoverSegment(basePath + i + "_" + subCounter, p);
        //            while (recLength < p.endOfStream)
        //            {
        //                createResidualPart(basePath + i, subCounter, recLength);
        //                subCounter++;
        //                p = new Program(basePath + i + "_" + subCounter);
        //                recLength = 0;
        //                recLength = recoverSegment(basePath + i + "_" + subCounter, p);
        //            }
        //        }
        //        catch (Exception e) { }
        //    }

        //}
        //long recoverSegment(string file, Program p)
        //{
        //    long recLength = 0;
        //    try
        //    {
        //        int dhtID = 1;
        //        Huffman.switchHuffman(dhtID);
        //        if (p.startDecoding())
        //        {
        //            //first dht is ok
        //            return p.finalizeDecoding();
        //        }

        //        Program p2to7 = new Program(file);
        //        Huffman.switchHuffman(2);
        //        while (!p2to7.startDecoding() && dhtID < 8)
        //        {
        //            dhtID++;
        //            Huffman.switchHuffman(dhtID);
        //            p = new Program(file);
        //        }
        //        // if all is consumed, then, return 1st dht (the standard table)
        //        recLength = (dhtID == 8) ? p.finalizeDecoding() : p2to7.finalizeDecoding();
        //    }
        //    catch (Exception e) { }
        //    return recLength;
        //}
        //void createResidualPart(String recFile, int subCounter, long seekVal)
        //{
        //    FileStream sourceStream = new FileStream(recFile + "_" + subCounter, FileMode.Open);
        //    sourceStream.Position = seekVal + 4 * 1024;
        //    FileStream targetStream = new FileStream(recFile + "_" + (subCounter + 1), FileMode.Create);
        //    byte[] buffer = new byte[sourceStream.Length - sourceStream.Position];
        //    sourceStream.Read(buffer, 0, buffer.Length);
        //    targetStream.Write(buffer, 0, buffer.Length);
        //    sourceStream.Close();
        //    targetStream.Close();
        //}


        //void testHuffman()
        //{
        //    Program p;
        //    //TextWriter tw = File.CreateText(@"C:\Users\eu3\Desktop\result7.txt");
        //    StreamReader sr = new StreamReader(@"I:\JpegRecovery\HuffmanImg\list.txt");
        //    String line;
        //    while ((line = sr.ReadLine()) != null)
        //    {
        //        p = new Program(line,0,0);
        //        p.startDecoding();
        //        //tw.WriteLine(line);
        //    }

        //    //tw.Close();
        //    sr.Close();
        //}

        //void writeFragment()
        //{
        //    FileStream fsr = new FileStream(@"C:\Users\eu3\Desktop\jpeg\DiskImage\nps-2009-canon2-gen6.raw", FileMode.Open);
        //    FileStream fsw = new FileStream(@"C:\Users\eu3\Desktop\jpeg\DiskImage\43_3.jpg", FileMode.OpenOrCreate);

        //    fsr.Seek(1556480, SeekOrigin.Begin);
        //    for (int i = 0; i < 9804; i++)
        //    {
        //        fsw.WriteByte((byte)fsr.ReadByte());
        //    }
        //    fsr.Seek(4407296, SeekOrigin.Begin);
        //    for (int i = 0; i < 786432; i++)
        //    {
        //        fsw.WriteByte((byte)fsr.ReadByte());
        //    }
        //    fsw.WriteByte(255);
        //    fsw.WriteByte(217);
        //    fsr.Close();
        //    fsw.Close();
        //    Console.WriteLine("yazdik aq");
        //}
        //public static void write2File(List<double[][]>[] rgbList, String prec)
        //{
        //    List<double[][]> dataList = rgbList[0];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox_Old\JpegRecovery\workspace\OrphanedFragmentDecoder\r.txt"))
        //    {
        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y].ToString(prec) + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }

        //    dataList = rgbList[1];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox_Old\JpegRecovery\workspace\OrphanedFragmentDecoder\g.txt"))
        //    {


        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y].ToString(prec) + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }

        //    dataList = rgbList[2];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox_Old\JpegRecovery\workspace\OrphanedFragmentDecoder\b.txt"))
        //    {


        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y].ToString(prec) + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }
        //    Console.WriteLine("done");
        //}

        //public static void write2File(List<int[][]>[] rgbList)
        //{
        //    List<int[][]> dataList = rgbList[0];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox\JpegRecovery\workspace\OrphanedFragmentDecoder\test_y.txt"))
        //    {
        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y] + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }

        //    dataList = rgbList[1];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox\JpegRecovery\workspace\OrphanedFragmentDecoder\test_cb.txt"))
        //    {


        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y] + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }

        //    dataList = rgbList[2];
        //    using (StreamWriter outfile = new StreamWriter(@"C:\Users\eu3\Dropbox\JpegRecovery\workspace\OrphanedFragmentDecoder\test_cr.txt"))
        //    {


        //        for (int x = 0; x < 8; x++)
        //        {
        //            string content = "";
        //            for (int k = 0; k < dataList.Count; k++)
        //            {

        //                for (int y = 0; y < 8; y++)
        //                {
        //                    content += dataList[k][x][y] + " ";
        //                }

        //            }
        //            //Console.WriteLine(content);
        //            outfile.WriteLine(content);
        //        }

        //    }
        //    Console.WriteLine("done");
        //}
    }
}
