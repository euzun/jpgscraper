using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

namespace JpegRecovery
{
    class RGBBuilder
    {
        public static long writePixels(List<double[][]>[] rgbList,int width,int chr,int offset, String fileName)
        {
            int blockCount = rgbList[0].Count + offset;
            int tail = (offset == 0) ? 0 : (chr == 0) ? width - (blockCount % width) : (chr == 3) ? 4 * width - (blockCount % 2 * width) : 2 * width - (blockCount % 2 * width);
            Console.WriteLine("Tail:" + tail);
            blockCount += tail;
            width *= (chr / 2 + 1);
            int height = (chr %2==1) ? 16 * (blockCount / (2*width)) : 8 * (blockCount / width);

            Bitmap bmp = new Bitmap(width * 8, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            List<double[][]> R,G,B;
            
            if (chr % 2 == 0)
            {
                R = rgbList[0].Take(width - offset).ToList();
                G = rgbList[1].Take(width - offset).ToList();
                B = rgbList[2].Take(width - offset).ToList();
                bmp = writeRowBlock(R, G, B, bmp, 0, offset);
                int skip = width - offset;

                for (int i = 1; i < height / 8 - 1; i++)
                {
                    R = rgbList[0].Skip(skip).Take(width).ToList();
                    G = rgbList[1].Skip(skip).Take(width).ToList();
                    B = rgbList[2].Skip(skip).Take(width).ToList();
                    bmp = writeRowBlock(R, G, B, bmp, i, 0);
                    skip += width;
                }

                R = rgbList[0].Skip(skip).ToList();
                G = rgbList[1].Skip(skip).ToList();
                B = rgbList[2].Skip(skip).ToList();
                bmp = writeRowBlock(R, G, B, bmp, height / 8 - 1, 0);
            }
            else
            {
                var rGroup = (chr == 1) ? rgbList[0].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 2 == 0).ToDictionary(g => g.Key, g => g) ://4:4:0
                    rgbList[0].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 4 == 0 || x.Index % 4 == 1).ToDictionary(g => g.Key, g => g);//4:2:0
                
                var gGroup = (chr == 1) ? rgbList[1].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 2 == 0).ToDictionary(g => g.Key, g => g) ://4:4:0
                    rgbList[1].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 4 == 0 || x.Index % 4 == 1).ToDictionary(g => g.Key, g => g);//4:2:0
                
                var bGroup = (chr == 1) ? rgbList[2].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 2 == 0).ToDictionary(g => g.Key, g => g) ://4:4:0
                    rgbList[2].Select((item, index) => new { Item = item, Index = index }).GroupBy(x => x.Index % 4 == 0 || x.Index % 4 == 1).ToDictionary(g => g.Key, g => g);//4:2:0
                
                
                var rEven = rGroup[true];
                var rOdd = rGroup[false];

                var gEven = gGroup[true];
                var gOdd = gGroup[false];

                var bEven = bGroup[true];
                var bOdd = bGroup[false];


                R = rEven.Select(x => x.Item).Take(width - offset).ToList();
                G = gEven.Select(x => x.Item).Take(width - offset).ToList();
                B = bEven.Select(x => x.Item).Take(width - offset).ToList();
                bmp = writeRowBlock(R, G, B, bmp, 0, offset);

                R = rOdd.Select(x => x.Item).Take(width - offset).ToList();
                G = gOdd.Select(x => x.Item).Take(width - offset).ToList();
                B = bOdd.Select(x => x.Item).Take(width - offset).ToList();
                bmp = writeRowBlock(R, G, B, bmp, 1, offset);

                int skip = width - offset;

                for (int i = 2; i < height / 8 - 2; i+=2)
                {
                    R = rEven.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    G = gEven.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    B = bEven.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    bmp = writeRowBlock(R, G, B, bmp, i, 0);

                    R = rOdd.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    G = gOdd.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    B = bOdd.Skip(skip).Select(x => x.Item).Take(width).ToList();
                    bmp = writeRowBlock(R, G, B, bmp, i+1, 0);
                    
                    skip += width;
                }

                R = rEven.Skip(skip).Select(x => x.Item).ToList();
                G = gEven.Skip(skip).Select(x => x.Item).ToList();
                B = bEven.Skip(skip).Select(x => x.Item).ToList();
                bmp = writeRowBlock(R, G, B, bmp, height / 8 - 2, 0);

                R = rOdd.Skip(skip).Select(x => x.Item).ToList();
                G = gOdd.Skip(skip).Select(x => x.Item).ToList();
                B = bOdd.Skip(skip).Select(x => x.Item).ToList();
                bmp = writeRowBlock(R, G, B, bmp, height / 8 - 1, 0);
                
            }



            FileStream stream = new FileStream(fileName, FileMode.Create);
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            long fileLength = stream.Length;
            stream.Close();
            return fileLength;
        }
        
        private static Bitmap writeRowBlock(List<double[][]> R, List<double[][]> G, List<double[][]> B, Bitmap bmp, int lastRow, int offset)
        {
            int iter=R.Count;
            for (int i = 0; i < iter;i++ )
            {
                for (int j = 0; j < 8; j++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        int x=(offset + i) * 8 + j;
                        int y = lastRow * 8 + k;
                        byte rp = (byte)(255 * R[i][k][j]);
                        byte gp = (byte)(255 * G[i][k][j]);
                        byte bp = (byte)(255 * B[i][k][j]);
                        try
                        {
                            bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(rp, gp, bp));
                        }
                        catch (Exception e) 
                        {   // Chroma SubSampling Error
                            return bmp; 
                        }
                    }
                }
            }
            return bmp;
        }
        
    }
}
