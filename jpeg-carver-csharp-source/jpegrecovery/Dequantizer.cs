using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegRecovery
{
    class Dequantizer
    {
        public static List<double[][]>[] ycc2rgbBlockList(List<int[][]>[] blockList,int chr_type)
        {
            /*Converts DCT coefficient matrix list to RGB coefficient matrix list
             * INPUTS:
             * blocklist: DCT coefficient matrix list
             * chr_type: Chroma subsampling method identifier.
             *          0--> 4:4:4       
             *          1--> 4:4:0
             *          2--> 4:2:2
             *          3--> 4:2:0
             */
            List<double[][]>[] yccBlockList = new List<double[][]>[] { new List<double[][]>(), new List<double[][]>(), new List<double[][]>() };
            double[][] idctBlock;
            int[][] dqt;
            int upsampling;
            for (int i = 0; i < 3;i++ )
            {
                dqt = (i == 0) ? Quantization.dqtY[chr_type] : Quantization.dqtC[chr_type];
                upsampling = blockList[0].Count / blockList[i].Count;
                for (int j = 0; j < blockList[i].Count; j++)
                {
                    idctBlock = idct(blockList[i][j], dqt);
                    for (int k = 0; k < upsampling;k++ )
                    {
                        yccBlockList[i].Add(idctBlock);
                    }
                }
            }
            return ycbcr2rgb(yccBlockList);
        }

        #region IDCT for 8x8 Block
        static double[][] idct(int[][] block, int[][] qt)
        {
            // Dot Product each coefficient with quatization value
            double[][] deqBlock = DotProduct(block, qt);

            // Matrix Multiplication of Tdctmtx,dequantized block above and dctmtx consequtively.
            double[][] idctBlock = MatrixProduct(MatrixProduct(Global.Tdctmtx, deqBlock), Global.dctmtx);

            // Add 128 to all coefficients
            idctBlock = SumWithScalar(idctBlock,128.0);
            return idctBlock;
        }


        static double[][] DotProduct(int[][] A, int[][] B)
        {
            double[][] C = new double[8][] { new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8] };
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    C[i][j] = A[i][j] * B[i][j];
                }
            }
            return C;
        }

        static double[][] MatrixProduct(double[][] A, double[][] B)
        {
            double[][] C = new double[8][] { new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8] };
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        C[i][j] += A[i][k] * B[k][j];
                    }
                }
            }

            return C;
        }

        static double[][] SumWithScalar(double[][] A, double B)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    A[i][j] = Math.Min(Math.Max(Math.Round(A[i][j] + B), 0), 255);
                }
            }

            return A;
        }

        #endregion

        #region YCbCr to RGB for 8x8 Block
        static List<double[][]>[] ycbcr2rgb(List<double[][]>[] yccBlockList) 
        {
            /*
             * R=Y+1.402*(Cr-128)
             * G=Y-0.34414*(Cb-128)-0.71414*(Cr-128)
             * B=Y+1.772*(Cb-128)
             */
            List<double[][]> rBlockList = new List<double[][]>();
            List<double[][]> gBlockList = new List<double[][]>();
            List<double[][]> bBlockList = new List<double[][]>();
            double[][] rBlock, gBlock, bBlock;
            try
            {
                for (int i = 0; i < yccBlockList[0].Count; i++)
                {
                    rBlock = new double[8][] { new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8] };
                    gBlock = new double[8][] { new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8] };
                    bBlock = new double[8][] { new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8], new double[8] };

                    for (int j = 0; j < 8; j++)
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            rBlock[j][k] = Math.Round(1.164383561643836 * yccBlockList[0][i][j][k] + 0.000000301124397 * yccBlockList[1][i][j][k] + 1.596026887335704 * yccBlockList[2][i][j][k] - 222.9216171091943);
                            gBlock[j][k] = Math.Round(1.164383561643836 * yccBlockList[0][i][j][k] - 0.391762539941450 * yccBlockList[1][i][j][k] - 0.812968292162205 * yccBlockList[2][i][j][k] + 135.5754095229665);
                            bBlock[j][k] = Math.Round(1.164383561643836 * yccBlockList[0][i][j][k] + 2.017232639556459 * yccBlockList[1][i][j][k] + 0.000003054261745 * yccBlockList[2][i][j][k] - 276.8363057950315);

                            rBlock[j][k] = Math.Min(Math.Max(rBlock[j][k], 0), 255) / 255.0;
                            gBlock[j][k] = Math.Min(Math.Max(gBlock[j][k], 0), 255) / 255.0;
                            bBlock[j][k] = Math.Min(Math.Max(bBlock[j][k], 0), 255) / 255.0;
                        }
                    }
                    rBlockList.Add(rBlock);
                    gBlockList.Add(gBlock);
                    bBlockList.Add(bBlock);
                }
            }catch(Exception e){}
            return new List<double[][]>[3] { rBlockList, gBlockList, bBlockList };
        }
        #endregion

    }
}
