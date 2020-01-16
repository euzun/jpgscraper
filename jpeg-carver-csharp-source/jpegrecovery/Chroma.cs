/* Detect Chroma Subsampling
* INPUT:
* blockListWith1Y--> blocklists of decoded stream as YCbCr     blockListWith1Y[0]--> Y blocks, blockListWith1Y[1]--> Cb blocks, blockListWith1Y[2]--> Cr blocks
* blockListWith2Y--> blocklists of decoded stream as YYCbCr    blockListWith2Y[0]--> Y blocks, blockListWith2Y[1]--> Cb blocks, blockListWith2Y[2]--> Cr blocks
* blockListWith4Y--> blocklists of decoded stream as YYYYCbCr  blockListWith4Y[0]--> Y blocks, blockListWith4Y[1]--> Cb blocks, blockListWith4Y[2]--> Cr blocks
* note: each block keeps an 8x8 DCT coefficient matrix
* 
* OUTPUT:
* 0--> 4:4:4       
* 1--> 4:4:0
* 2--> 4:2:2
* 3--> 4:2:0
* 
* AUTHOR   : ERKAM UZUN
* DATE     : 03/26/2015
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegRecovery
{
    class Chroma
    {
        List<int[][]>[] blockListWith1Y = new List<int[][]>[] { new List<int[][]>(), new List<int[][]>(), new List<int[][]>() };
        List<int[][]>[] blockListWith2Y = new List<int[][]>[] { new List<int[][]>(), new List<int[][]>(), new List<int[][]>() };
        List<int[][]>[] blockListWith4Y = new List<int[][]>[] { new List<int[][]>(), new List<int[][]>(), new List<int[][]>() };
        int[] featureFlag;
        public Chroma(List<int[][]>[] blockListWith1Y, List<int[][]>[] blockListWith2Y, List<int[][]>[] blockListWith4Y, int[] invalid_codeword, int[] overflow_codeword)
        {
            this.featureFlag = new int[3] { invalid_codeword[0] | overflow_codeword[0], invalid_codeword[1] | overflow_codeword[1], invalid_codeword[2] | overflow_codeword[2] };
            this.blockListWith1Y = blockListWith1Y;
            this.blockListWith2Y = blockListWith2Y;
            this.blockListWith4Y = blockListWith4Y;
            
        }
        public int chromaSubsampling()
        {
            /* Detect Chroma Subsampling
             * Set General.chr:
             * 0--> 4:4:4       
             * 1--> 4:4:0
             * 2--> 4:2:2
             * 3--> 4:2:0
             */

            int mcu = mcuStructure();
            Console.WriteLine("MCU: "+mcu);
            switch (mcu)
            {
                case 0:
                    Global.yccList = blockListWith1Y;
                    Global.chr = 0;
                    Global.rgbList = Dequantizer.ycc2rgbBlockList(Global.yccList, 0);
                    break;
                case 1:
                    Global.yccList = blockListWith2Y;
                    Global.chr = layout();
                    // if chr==1 General.rgbList is already setted in layout() method
                    break;
                default:
                    Global.yccList = blockListWith4Y;
                    Global.chr = 3;
                    Global.rgbList = Dequantizer.ycc2rgbBlockList(Global.yccList, 3);
                    break;
            }
            return mcu;

        }
        int mcuStructure()
        {
            /* Detect MCU Structure
             * OUTPUT
             * 0-->YCbCr        No-Sampling 
             * 1-->YYCbCr       Vertical or Horizontal Sampling    
             * 2-->YYYYCbCr     Vertical and Horizontal Sampling
             */
            return (featureFlag.Sum() == 2) ? Array.IndexOf(featureFlag, 0) : featureDecision();
        }

        #region DECISION ARRAY
        int featureDecision()
        {
            /* Detect MCU Structure by using each feature
             * OUTPUT: int[] array with 5 decision for each feature
             * DECISIONS:
             * 0-->YCbCr        No-Sampling 
             * 1-->YYCbCr       Vertical or Horizontal Sampling    
             * 2-->YYYYCbCr     Vertical and Horizontal Sampling
             */

            /* Gets all 5 features for each Y,Cb and Cr components. exp: featuresWith1Y[1,2] returns Cb's coeffMean
             * Features' order: DCMEAN, ACMEAN, COEFFMEAN, VARMEAN,VARMAX
             */
            int maxInt = int.MaxValue/3-1;
            double[] maxIntArray = new double[5] { maxInt, maxInt, maxInt, maxInt, maxInt };
            double[][] featuresWith1Y = new double[3][] { maxIntArray, maxIntArray, maxIntArray };
            double[][] featuresWith2Y = new double[3][] { maxIntArray, maxIntArray, maxIntArray };
            double[][] featuresWith4Y = new double[3][] { maxIntArray, maxIntArray, maxIntArray };

            if (featureFlag[0] == 1)
            {
                int sub1YSize = Math.Min(Global.maxWidth, blockListWith1Y[0].Count);
                featuresWith1Y = new double[][] { compFeatures(blockListWith1Y[0].Take(sub1YSize).ToList()), compFeatures(blockListWith1Y[1].Take(sub1YSize).ToList()), compFeatures(blockListWith1Y[2].Take(sub1YSize).ToList()) };
            }
            if (featureFlag[1] == 1)
            {
                int sub2YSize = Math.Min(Global.maxWidth, blockListWith2Y[0].Count);
                featuresWith2Y = new double[][] { compFeatures(blockListWith2Y[0].Take(sub2YSize).ToList()), compFeatures(blockListWith2Y[1].Take(sub2YSize / 2).ToList()), compFeatures(blockListWith2Y[2].Take(sub2YSize / 2).ToList()) };
            }

            if (featureFlag[2] == 1)
            {
                int sub4YSize = Math.Min(Global.maxWidth, blockListWith4Y[0].Count);
                featuresWith4Y = new double[][] { compFeatures(blockListWith4Y[0].Take(sub4YSize).ToList()), compFeatures(blockListWith4Y[1].Take(sub4YSize / 4).ToList()), compFeatures(blockListWith4Y[2].Take(sub4YSize / 4).ToList()) };
            }
            // DCMEAN
            double[] dcMean = new double[] { 
                featuresWith1Y[0][0] + featuresWith1Y[1][0] + featuresWith1Y[2][0],
                featuresWith2Y[0][0] + featuresWith2Y[1][0] + featuresWith2Y[2][0],
                featuresWith4Y[0][0] + featuresWith4Y[1][0] + featuresWith4Y[2][0]
            };

            // ACMEAN
            double[] acMean = new double[] { 
                /*featuresWith1Y[0][1] +*/ featuresWith1Y[1][1] + featuresWith1Y[2][1],
                /*featuresWith2Y[0][1] +*/ featuresWith2Y[1][1] + featuresWith2Y[2][1],
                /*featuresWith4Y[0][1] +*/ featuresWith4Y[1][1] + featuresWith4Y[2][1]
            };

            // COEFFMEAN
            double[] coeffMean = new double[] { 
                featuresWith1Y[0][2] + featuresWith1Y[1][2] + featuresWith1Y[2][2],
                featuresWith2Y[0][2] + featuresWith2Y[1][2] + featuresWith2Y[2][2],
                featuresWith4Y[0][2] + featuresWith4Y[1][2] + featuresWith4Y[2][2]
            };

            // VARMEAN
            double[] varMean = new double[] { 
                /*featuresWith1Y[0][3] +*/ featuresWith1Y[1][3] + featuresWith1Y[2][3],
                /*featuresWith2Y[0][3] +*/ featuresWith2Y[1][3] + featuresWith2Y[2][3],
                /*featuresWith4Y[0][3] +*/ featuresWith4Y[1][3] + featuresWith4Y[2][3]
            };

            // VARMAX
            double[] varMax = new double[] { 
                /*featuresWith1Y[0][4] +*/ featuresWith1Y[1][4] + featuresWith1Y[2][4],
                /*featuresWith2Y[0][4] +*/ featuresWith2Y[1][4] + featuresWith2Y[2][4],
                /*featuresWith4Y[0][4] +*/ featuresWith4Y[1][4] + featuresWith4Y[2][4]
            };


            int[] decisionArray = new int[3];
            decisionArray[Array.IndexOf(dcMean,dcMean.Min())]++;
            decisionArray[Array.IndexOf(acMean,acMean.Min())]++;
            decisionArray[Array.IndexOf(coeffMean,coeffMean.Min())]++;
            decisionArray[Array.IndexOf(varMean,varMean.Min())]++;
            decisionArray[Array.IndexOf(varMax, varMax.Min())]++;

            return Array.IndexOf(decisionArray, decisionArray.Max());
        }
 
        #endregion

        #region FEATURES
        double[] compFeatures(List<int[][]> blockList)
        {
            /*Calculates dcMean,acMean,coeffMean,mean of block variences,max of block variences
             */
            int[][] block;
            int counterDC = 0, counterNonZeroDC = 0, counterAC = 0;
            double sumDC = 0, sumAC = 0,sumCOEFF=0, negMean=0;
            
            List<int> acList = new List<int>();
            double[] acVariance=new double[blockList.Count];

            for (int i = 0; i < blockList.Count; i++)
            {
                block = new int[8][];
                for (int j = 0; j < 8;j++ )
                {
                    block[j]=new int[8];
                    Array.Copy(blockList[i][j], block[j], 8);
                }

                sumDC += Math.Abs(block[0][0]);
                counterDC++;
                counterNonZeroDC += (block[0][0]==0) ? 0 : 1;

                block[0][0] = 0;
                for (int j = 0; j < 8; j++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        sumAC += Math.Abs(block[j][k]);
                        counterAC += (block[j][k] == 0) ? 0 : 1;
                        if (j + k > 0)
                        {
                            acList.Add(block[j][k]);
                        }
                    }
                }

                negMean=acList.Average();
                acVariance[i] = acList.Sum(number => Math.Pow(number - negMean, 2.0)) / 63.0;
                acList.Clear();
            }
            sumCOEFF = sumDC + sumAC;
            double dcMean = (sumDC > 0) ? sumDC / counterDC : int.MaxValue;
            double acMean = (sumAC > 0) ? sumAC / counterAC : int.MaxValue;
            double coeffMean = (sumCOEFF > 0) ? (sumCOEFF / (counterNonZeroDC + counterAC)) : int.MaxValue;
            double varMean = (acVariance.Count() > 0) ? acVariance.Average() : int.MaxValue;
            double varMax = (acVariance.Count() > 0) ? acVariance.Max() : int.MaxValue;

            return new double[]{dcMean,acMean,coeffMean,varMean,varMax};
        }
        #endregion

        #region YYCbCr LAYOUT DETECTION
        int layout()
        {
            // build rgbList as 4:4:0
            List<double[][]>[] verList = Dequantizer.ycc2rgbBlockList(Global.yccList, 1);
            // build rgbList as 4:2:2
            List<double[][]>[] horList = Dequantizer.ycc2rgbBlockList(Global.yccList, 2);

            List<double[]> verDiff = new List<double[]>();// 0->tbDiff, 1->lrDiff
            List<double[]> horDiff = new List<double[]>();// 0->tbDiff, 1->lrDiff

            for (int i = 0; i < verList[0].Count; i += 2)
            {
                verDiff.Add(borderDiff(new double[][][] { verList[0][i], verList[1][i], verList[2][i] }, new double[][][] { verList[0][i + 1], verList[1][i + 1], verList[2][i + 1] }));
            }

            for (int i = 0; i < horList[0].Count; i += 2)
            {
                horDiff.Add(borderDiff(new double[][][] { horList[0][i], horList[1][i], horList[2][i] }, new double[][][] { horList[0][i + 1], horList[1][i + 1], horList[2][i + 1] }));
            }


            if ((verDiff.Average(i => i[0]) < horDiff.Average(i => i[1])) && (verDiff.Average(i => i[1]) > horDiff.Average(i => i[0])))
            {
                Global.rgbList = verList;
                return 1;
            }

            Global.rgbList = horList;
            return 2;
        }

        double[] borderDiff(double[][][] F, double[][][] S)
        {
            // 0th->Diff between bottom of the F and top of the S arrays
            // 1th->Diff between right of the F and left of the S arrays
            double sumTB = 0, sumLR = 0;
            for (int i = 0; i < 8; i++)
            {
                sumTB += Math.Sqrt(sqr(F[0][7][i] - S[0][0][i]) + sqr(F[1][7][i] - S[1][0][i]) + sqr(F[2][7][i] - S[2][0][i]));
                sumLR += Math.Sqrt(sqr(F[0][i][7] - S[0][i][0]) + sqr(F[1][i][7] - S[1][i][0]) + sqr(F[2][i][7] - S[2][i][0]));
            }
            return new double[] { sumTB / 8.0, sumLR / 8.0 };
        }

        double sqr(double x)
        {
            return x * x;
        }

        #endregion
    
    }
}
