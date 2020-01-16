using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegRecovery
{
    class Offset
    {
        public int imageOffset(List<double[][]>[] rgbList,int width,int chr)
        {
            //if (chr % 2 == 1)
            //{
            //    RGBBuilder rgbBuild=new RGBBuilder();
            //    rgbList = rgbBuild.buildRGB(rgbList, width, chr);
            //}

            width *= (chr / 2 + 1);
            int height =Math.Min(50, rgbList[0].Count / width );
            List<double[][]> LR, LG, LB, RR, RG, RB;
            List<double> verDiff = new List<double>();

            if (chr % 2 == 0)
            {
                //4:4:4 || 4:2:2
                int iter = width - 1;
                LR = rgbList[0].Where((item, index) => index % width == width - 1).Take(height - 1).ToList();
                LG = rgbList[1].Where((item, index) => index % width == width - 1).Take(height - 1).ToList();
                LB = rgbList[2].Where((item, index) => index % width == width - 1).Take(height - 1).ToList();

                RR = rgbList[0].Skip(width).Where((item, index) => index % width == 0).Take(height - 1).ToList();
                RG = rgbList[1].Skip(width).Where((item, index) => index % width == 0).Take(height - 1).ToList();
                RB = rgbList[2].Skip(width).Where((item, index) => index % width == 0).Take(height - 1).ToList();

                verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));

                for (int i = 0; i < iter; i++)
                {
                    LR = rgbList[0].Where((item, index) => index % width == i).Take(height).ToList();
                    LG = rgbList[1].Where((item, index) => index % width == i).Take(height).ToList();
                    LB = rgbList[2].Where((item, index) => index % width == i).Take(height).ToList();

                    RR = rgbList[0].Where((item, index) => index % width == i + 1).Take(height).ToList();
                    RG = rgbList[1].Where((item, index) => index % width == i + 1).Take(height).ToList();
                    RB = rgbList[2].Where((item, index) => index % width == i + 1).Take(height).ToList();

                    verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));
                }
            
                return width-verDiff.IndexOf(verDiff.Max());
            }
            else if(chr==1)
            {
                //4:4:0
                int LI1, LI2, RI1, RI2;
                int iter = width - 2;

                LR = rgbList[0].Where((item, index) => index % (2 * width) == 2 * width - 2 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();
                LG = rgbList[1].Where((item, index) => index % (2 * width) == 2 * width - 2 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();
                LB = rgbList[2].Where((item, index) => index % (2 * width) == 2 * width - 2 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();

                RR = rgbList[0].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 1).Take(height).Skip(1).ToList();
                RG = rgbList[1].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 1).Take(height).Skip(1).ToList();
                RB = rgbList[2].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 1).Take(height).Skip(1).ToList();

                verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));
                
                for (int i = 0; i < iter; i++)
                {
                    LI1 = 2 * i;
                    LI2 = 2 * i + 1;
                    LR = rgbList[0].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();
                    LG = rgbList[1].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();
                    LB = rgbList[2].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();

                    RI1 = 2 * (i + 1);
                    RI2 = 2 * (i + 1) + 1;
                    RR = rgbList[0].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();
                    RG = rgbList[1].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();
                    RB = rgbList[2].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();

                    verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));
                }
                return width - verDiff.IndexOf(verDiff.Max());
            }
            else
            {
                // 4:2:0
                int LI1, LI2, RI1, RI2;
                int iter = width - 2;

                LR = rgbList[0].Where((item, index) => index % (2 * width) == 2 * width - 3 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();
                LG = rgbList[1].Where((item, index) => index % (2 * width) == 2 * width - 3 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();
                LB = rgbList[2].Where((item, index) => index % (2 * width) == 2 * width - 3 || index % (2 * width) == 2 * width - 1).Take(height - 1).ToList();

                RR = rgbList[0].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 2).Take(height).Skip(1).ToList();
                RG = rgbList[1].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 2).Take(height).Skip(1).ToList();
                RB = rgbList[2].Where((item, index) => index % (2 * width) == 0 || index % (2 * width) == 2).Take(height).Skip(1).ToList();

                verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));

                for (int i = 0; i < iter; i++)
                {
                    LI1 = 2 * i - i % 2;
                    LI2 = 2 * (i + 1) - i % 2;
                    LR = rgbList[0].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();
                    LG = rgbList[1].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();
                    LB = rgbList[2].Where((item, index) => index % (2 * width) == LI1 || index % (2 * width) == LI2).Take(height).ToList();

                    RI1 = 2 * (i + 1) - (i + 1) % 2;
                    RI2 = 2 * (i + 2) - (i + 1) % 2;
                    RR = rgbList[0].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();
                    RG = rgbList[1].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();
                    RB = rgbList[2].Where((item, index) => index % (2 * width) == RI1 || index % (2 * width) == RI2).Take(height).ToList();

                    verDiff.Add(LRDiffMean(LR, LG, LB, RR, RG, RB));
                }
                return width - verDiff.IndexOf(verDiff.Max());
            }
        }

        private double LRDiffMean(List<double[][]> LR, List<double[][]> LG, List<double[][]> LB, List<double[][]> RR, List<double[][]> RG, List<double[][]> RB)
        {
            int mL = Math.Min(LR.Count,RR.Count);
            double cumSum = 0;
            for (int i = 0; i < mL; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    cumSum += Math.Sqrt(sqr(LR[i][j][7] - RR[i][j][0]) + sqr(LG[i][j][7] - RG[i][j][0]) + sqr(LB[i][j][7] - RB[i][j][0]));
                }
            }
            return cumSum / (mL * 8.0);
        }

        private double sqr(double x)
        {
            return x * x;
        }
    }
}
