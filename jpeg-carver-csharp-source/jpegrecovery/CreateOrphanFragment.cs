using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegRecovery
{
    class CreateOrphanFragment
    {
        public static void toFileFragments()
        {
            StreamReader sr = new StreamReader(@"I:\JpegRecovery\HuffmanImg\list.txt");
            String line;
            while ((line = sr.ReadLine()) != null)
            {
                createFragmentFile(line);
            }

            sr.Close();
            Console.WriteLine("yazdik aq");
        }
        public static void createFragmentFile(string file){
            String rFile = file+".jpg";
            FileStream fsr= new FileStream(rFile, FileMode.Open);
            FileInfo fi = new FileInfo(rFile);
            FileStream fsw = new FileStream(file, FileMode.OpenOrCreate);

            fsr.Seek(1024*10,SeekOrigin.Begin);
            long l=fsr.Length-4;
            for (int i = 0; i < 64 * 1024 && fsr.Position<l;i++ )
            {
                fsw.WriteByte((byte)fsr.ReadByte());
            }
            fsr.Close();
            fsw.Close();
        }
    }
}
