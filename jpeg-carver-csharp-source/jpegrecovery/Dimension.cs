using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Differentiation;
using System.Numerics;

namespace JpegRecovery
{
    class Dimension
    {
        public int[] wlist = new int[5];
        public int imageWidth()
        {
            int subImgSize = Math.Min(Global.maxWidth*3, Global.rgbList[0].Count);
            int chrSize = subImgSize / (int)Math.Pow(2, (Global.chr + 1) / 2);
            List<double[][]>[] rgbSubList = { Global.rgbList[0].Take(subImgSize).ToList(), Global.rgbList[1].Take(subImgSize).ToList(), Global.rgbList[2].Take(subImgSize).ToList() };
            List<int[][]>[] yccSubList = { Global.yccList[0].Take(subImgSize).ToList(), Global.yccList[1].Take(chrSize).ToList(), Global.yccList[2].Take(chrSize).ToList() };
            
            List<double> dArray = mcuDifferenceArray(rgbSubList, Global.chr);
            int w1 = method1(dArray);
            int w2 = method2(dArray);
            int w3 = method3(rgbSubList, Global.chr);
            int w4 = method4(yccSubList, Global.chr);
            int w5 = method5(rgbSubList, Global.chr);
            int wfinal = finalWidth(Global.rgbList, w1, w2, w3, w4, w5);// *(Global.chr / 2 + 1) * 8;
            

            wlist[0] = (w1 == 0) ? 20 : w1;
            wlist[1] = (w2 == 0) ? 21 : w2;
            wlist[2] = (w3 == 0) ? 22 : w3;
            wlist[3] = (w4 == 0) ? 23 : w4;
            wlist[4] = (w5 == 0) ? 24 : w5;
            wfinal = wfinal < 20 ? 25 : wfinal;
            Console.WriteLine("WIDTHS-> "+ wlist[0] + ";" + wlist[1] + ";" + wlist[2] + ";" + wlist[3] + ";" + wlist[4] + ";" + wfinal);
            return wfinal;
        }

        #region MCU DIFFRENCE ARRAY
        private List<double> mcuDifferenceArray(List<double[][]>[] rgbList, int chr)
        {
            /* Chroma Subsampling
             * 0--> 4:4:4       
             * 1--> 4:4:0
             * 2--> 4:2:2
             * 3--> 4:2:0
             */
            List<double> mcuDiffArray = new List<double>();
            int listSize = 0;
            switch (chr)
            {
                case 0:
                    listSize = rgbList[0].Count - 1;
                    for (int i = 0; i < listSize; i++)
                    {
                        mcuDiffArray.Add(LRDiffSum(new double[][][] { rgbList[0][i], rgbList[1][i], rgbList[2][i] }, new double[][][] { rgbList[0][i + 1], rgbList[1][i + 1], rgbList[2][i + 1] }));
                    }
                    break;
                case 1:
                    listSize = rgbList[0].Count / 2 - 1;
                    for (int i = 0; i < 2 * listSize; i += 2)
                    {
                        mcuDiffArray.Add((
                                        LRDiffSum(new double[][][] { rgbList[0][i], rgbList[1][i], rgbList[2][i] }, new double[][][] { rgbList[0][i + 2], rgbList[1][i + 2], rgbList[2][i + 2] }) +
                                        LRDiffSum(new double[][][] { rgbList[0][i + 1], rgbList[1][i + 1], rgbList[2][i + 1] }, new double[][][] { rgbList[0][i + 3], rgbList[1][i + 3], rgbList[2][i + 3] })
                                     ) / 2.0);
                    }
                    break;
                case 2:
                    listSize = rgbList[0].Count / 2 - 1;
                    for (int i = 1; i < 2 * listSize; i += 2)
                    {
                        mcuDiffArray.Add(LRDiffSum(new double[][][] { rgbList[0][i], rgbList[1][i], rgbList[2][i] }, new double[][][] { rgbList[0][i + 1], rgbList[1][i + 1], rgbList[2][i + 1] }));
                    }
                    break;
                case 3:
                    listSize = rgbList[0].Count / 4 - 1;
                    for (int i = 1; i < 4 * listSize; i += 4)
                    {
                        mcuDiffArray.Add((
                                        LRDiffSum(new double[][][] { rgbList[0][i], rgbList[1][i], rgbList[2][i] }, new double[][][] { rgbList[0][i + 3], rgbList[1][i + 3], rgbList[2][i + 3] }) +
                                        LRDiffSum(new double[][][] { rgbList[0][i + 2], rgbList[1][i + 2], rgbList[2][i + 2] }, new double[][][] { rgbList[0][i + 5], rgbList[1][i + 5], rgbList[2][i + 5] })
                                    ) / 2.0);
                    }
                    break;
            }
            return mcuDiffArray;
        }

        private double LRDiffSum(double[][][] F, double[][][] S)
        {
            // Return diff between right of the F and left of the S arrays
            double sumLR = 0;
            for (int i = 0; i < 8; i++)
            {
                sumLR += Math.Sqrt(sqr(F[0][i][7] - S[0][i][0]) + sqr(F[1][i][7] - S[1][i][0]) + sqr(F[2][i][7] - S[2][i][0]));
            }
            return sumLR / 8.0;
        }

        private double sqr(double x)
        {
            return x * x;
        }

        #endregion

        #region METHOD 1 - AUTOCORRELATION OF MCU DIFFERENCE ARRAY
        private int method1(List<double> dArray)
        {
            /* Autocorrelation of mcu difference array 
             * Wr(bda) in the journal
             */
            List<double> acorr = autocorr(dArray);
            List<int> peaks = findPeaks(acorr, 4.0);
            peaks = (peaks.Count < 4) ? findPeaks(acorr, 8.0) : peaks;
            peaks = (peaks.Count < 4) ? findPeaks(acorr, 16.0) : peaks;

            List<int> diff = getDiff(peaks, 4);

            return Mode(diff);
        }

        private List<double> autocorr(List<double> x)
        {
            Complex[] cx = x.ConvertAll(i => new Complex(i, 0)).Concat(new Complex[x.Count - 1]).ToArray();
            Fourier.Forward(cx, FourierOptions.Matlab);
            cx = Array.ConvertAll(cx, i => new Complex(sqr(i.Magnitude), 0));
            Fourier.Inverse(cx, FourierOptions.Matlab);
            double[] acorr = Array.ConvertAll<Complex, Double>(cx, i => i.Magnitude);
            return acorr.Skip(x.Count).Concat(acorr.Take(x.Count)).ToList<double>();
        }

        private List<int> findPeaks(List<double> x, double threshPer)
        {
            //Create peakLoc and peakMag lists
            List<int> peakLoc = new List<int>();
            //List<double> peakMag = new List<double>();

            List<double> dX = derivative(x).ToList();//Find derivative
            dX = dX.Select(i => { if (i == 0) i = -2.2204e-16; return i; }).ToList(); //This is so we find the first of repeated values


            //Find where the derivative changes sign            
            List<double> pX = dX.Zip(dX.Skip(1), (a, b) => a * b).ToList();
            List<int> ind = new List<int>();

            for (int i = 0; i < pX.Count; i++)
            {
                if (pX[i] < 0)
                    ind.Add(i + 1);
            }

            pX.Clear();
            dX.Clear();

            //Include endpoints in potential peaks and valleys is desired
            ind.Insert(0, 0);
            ind.Add(x.Count - 1);
            ind.ForEach(i => { pX.Add(x[i]); });

            //returns the indicies of local maxima that are at least sel above surrounding data
            double sel = (x.Max() - x.Min()) / threshPer;

            // pX only has the peaks, valleys, and start-end points
            int len = pX.Count;
            double minMag = pX.Min();

            if (len > 2)//Function with peaks and valleys
            {
                //Set initial parameters for loop
                double tempMag = minMag;
                bool foundPeak = false;
                double leftMin = minMag;

                #region Deal with first point
                /* Deal with first point a little differently since tacked it on
                 * Calculate the sign of the derivative since we took the first point
                 * on it does not neccessarily alternate like the rest.
                 */
                dX = derivative(pX.GetRange(0, 3)).ToList();
                if (Math.Sign(dX[0]) <= 0)//The first point is larger or equal to the second
                {
                    if (Math.Sign(dX[0]) == Math.Sign(dX[1]))
                    {//Want alternating signs
                        pX.RemoveAt(1);
                        ind.RemoveAt(1);
                        len--;
                    }
                }
                else//The first point is smaller than the second
                {
                    if (Math.Sign(dX[0]) == Math.Sign(dX[1]))
                    {//Want alternating signs
                        pX.RemoveAt(0);
                        ind.RemoveAt(0);
                        len--;
                    }
                }
                #endregion

                //Skip the first point if it is smaller so we always start on a maxima
                int ii = (pX[0] > pX[1]) ? -1 : 0;

                int tempLoc = 0;

                while (ii < len)
                {
                    ii++;//This is a peak
                    // Reset peak finding if we had a peak and the next peak is bigger than the last or the left min was small enough to reset.

                    if (foundPeak)
                    {
                        tempMag = minMag;
                        foundPeak = false;
                    }

                    // Make sure we don't iterate past the length of our vector
                    if (ii == len)
                        break; //We assign the last point differently out of the loop


                    //Found new peak that was larger than tempMag and sel larger than the minimum to its left (leftMin).
                    if (pX[ii] > tempMag && pX[ii] > leftMin + sel)
                    {
                        tempLoc = ii;
                        tempMag = pX[ii];
                    }

                    ii++;//Move onto the valley
                    if (ii == len)
                        break;
                    //Come down at least sel from peak
                    if (!foundPeak && (tempMag > sel + pX[ii]))
                    {
                        foundPeak = true; //We have found a peak
                        leftMin = pX[ii];

                        peakLoc.Add(ind[tempLoc]);// Add peak to index
                        //peakMag.Add(tempMag);
                    }
                    else if (pX[ii] < leftMin)
                    {//New left minima
                        leftMin = pX[ii];
                    }
                }

                //Check end point
                if (pX.Last() > tempMag && pX.Last() > leftMin + sel)  // x(end) > tempMag && x(end) > leftMin + sel
                {
                    peakLoc.Add(ind[len]);
                    //peakMag.Add(pX.Last());
                }
                else if (!foundPeak && tempMag > minMag)//~foundPeak && tempMag > minMag % Check if we still need to add the last point
                {
                    peakLoc.Add(ind[tempLoc]);
                    //peakMag.Add(tempMag);
                }

            }
            //else//This is a monotone function where an endpoint is the only peak
            //{
            //    //            [peakMags,xInd] = max(x);
            //    //if peakMags > minMag + sel
            //    //    peakInds = ind(xInd);
            //    //else
            //    //    peakMags = [];
            //    //    peakInds = [];
            //    //end
            //}
            return peakLoc;
        }

        private IEnumerable<double> derivative(IEnumerable<double> x)
        {
            return x.Zip(x.Skip(1), (a, b) => b - a);
        }

        private List<int> getDiff(List<int> array, int thr)
        {
            List<int> diff = new List<int>();
            int acount = array.Count;
            int adiff = 0;
            for (int i = 0; i < acount; i++)
            {
                for (int j = i + 1; j < acount; j++)
                {
                    if ((adiff = array[j] - array[i]) > thr)
                        diff.Add(adiff);
                }
            }
            return diff;
        }

        private T Mode<T>(IEnumerable<T> list)
        {
            // Initialize the return value
            T mode = default(T);

            // Test for a null reference and an empty list
            if (list != null && list.Count() > 0)
            {
                // Store the number of occurences for each element
                Dictionary<T, int> counts = new Dictionary<T, int>();

                // Add one to the count for the occurence of a character
                foreach (T element in list)
                {
                    if (counts.ContainsKey(element))
                        counts[element]++;
                    else
                        counts.Add(element, 1);
                }

                // Loop through the counts of each element and find the 
                // element that occurred most often
                int max = 0;

                foreach (KeyValuePair<T, int> count in counts)
                {
                    if (count.Value > max)
                    {
                        // Update the mode
                        mode = count.Key;
                        max = count.Value;
                    }
                }
            }

            return mode;
        }
        #endregion

        #region METHOD 2 - K-MEANS OF MCU DIFFERENCE ARRAY
        private int method2(List<double> dArray)
        {
            /* 2-Means of mcu difference array 
             * Wr(kmeans) in the journal
             */
            int[] idx = KMeans.Cluster(dArray, 2);
            List<int> bound = getBound(idx);
            if (bound.Count > 1000)
            {
                List<double> subMean = new List<double>();
                for (int i = 0; i < bound.Count; i++)
                {
                    subMean.Add(dArray[bound[i]]);
                }
                idx = KMeans.Cluster(subMean, 2);
                bound = getBound(idx);
            }
            List<int> diff = getDiff(bound, 1);

            return Mode(diff);
        }

        private List<int> getBound(int[] idx)
        {
            int ones = idx.Sum();
            int zeros = idx.Length - ones;

            int bindex = (ones < zeros) ? 1 : 0;

            List<int> bound = new List<int>();

            for (int i = 0; i < idx.Length; i++)
            {
                if (idx[i] == bindex)
                    bound.Add(i);
            }

            return bound;
        }

        #endregion

        #region METHOD 3 - HORIZONTAL MASK FIT
        private int method3(List<double[][]>[] rgbList, int chr)
        {
            /* Chroma Subsampling
             * 0--> 4:4:4       
             * 1--> 4:4:0
             * 2--> 4:2:2
             * 3--> 4:2:0
             */

            List<int> w_cand = new List<int>();
            List<double> w_temp = new List<double>();

            int mask_size = 40;//mcu
            int mask_offset = 4;//mcu
            int wind_size = Math.Min(Global.maxWidth, rgbList[0].Count);
            int mask_skip = 1;
            int sub_skip = 1;

            List<double[][]> MR, MG, MB;
            List<double[][]> SR, SG, SB;
            switch (chr)
            {
                case 0:
                    // 4:4:4
                    for (int ii = 0; ii < 7; ii++)
                    {
                        MR = rgbList[0].Skip(mask_offset).Take(mask_size).ToList();
                        MG = rgbList[1].Skip(mask_offset).Take(mask_size).ToList();
                        MB = rgbList[2].Skip(mask_offset).Take(mask_size).ToList();

                        for (int i = mask_offset + mask_size; i < wind_size - mask_size; i += sub_skip)
                        {
                            SR=rgbList[0].Skip(i).Take(mask_size).ToList();
                            SG=rgbList[1].Skip(i).Take(mask_size).ToList();
                            SB=rgbList[2].Skip(i).Take(mask_size).ToList();

                            w_temp.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));
                        }
                        w_cand.Add(mask_size + w_temp.IndexOf(w_temp.Min()));
                        w_temp.Clear();
                        mask_offset += mask_skip;
                    }
                    break;
                case 1:
                    //4:4:0
                    sub_skip = 2;
                    mask_skip = 2;
                    for (int ii = 0; ii < 7; ii++)
                    {
                        MR = rgbList[0].Skip(mask_offset).Where((item, index) => index % 2 != 0).Take(mask_size).ToList();
                        MG = rgbList[1].Skip(mask_offset).Where((item, index) => index % 2 != 0).Take(mask_size).ToList();
                        MB = rgbList[2].Skip(mask_offset).Where((item, index) => index % 2 != 0).Take(mask_size).ToList();
                        
                        for (int i = mask_offset + mask_size; i < wind_size - mask_size; i += sub_skip)
                        {
                            SR=rgbList[0].Skip(i).Where((item, index) => index % 2 == 0).Take(mask_size).ToList();
                            SG=rgbList[1].Skip(i).Where((item, index) => index % 2 == 0).Take(mask_size).ToList();
                            SB=rgbList[2].Skip(i).Where((item, index) => index % 2 == 0).Take(mask_size).ToList();

                            w_temp.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));
                        }
                        w_cand.Add(mask_size/2 + w_temp.IndexOf(w_temp.Min()));
                        w_temp.Clear();
                        mask_offset += mask_skip;
                    }
                    break;
                case 2:
                    //4:2:2
                    sub_skip = 2;
                    mask_skip = 2;
                    for (int ii = 0; ii < 7; ii++)
                    {
                        MR = rgbList[0].Skip(mask_offset).Take(mask_size).ToList();
                        MG = rgbList[1].Skip(mask_offset).Take(mask_size).ToList();
                        MB = rgbList[2].Skip(mask_offset).Take(mask_size).ToList();

                        for (int i = mask_offset + mask_size; i < wind_size - mask_size; i += sub_skip)
                        {
                            SR=rgbList[0].Skip(i).Take(mask_size).ToList();
                            SG=rgbList[1].Skip(i).Take(mask_size).ToList();
                            SB=rgbList[2].Skip(i).Take(mask_size).ToList();
                            w_temp.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));
                        }

                        w_cand.Add(mask_size/2 + w_temp.IndexOf(w_temp.Min()));
                        w_temp.Clear();
                        mask_offset += mask_skip;
                    }
                    break;
                case 3:
                    //4:2:0
                    sub_skip = 4;
                    mask_skip = 4;

                    for (int ii = 0; ii < 7; ii++)
                    {

                        MR = rgbList[0].Skip(mask_offset).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(mask_size).ToList();
                        MG = rgbList[1].Skip(mask_offset).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(mask_size).ToList();
                        MB = rgbList[2].Skip(mask_offset).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(mask_size).ToList();

                        for (int i = mask_offset + 2 * mask_size; i < wind_size - 2 * mask_size; i += sub_skip)
                        {

                            SR = rgbList[0].Skip(i).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(mask_size).ToList();
                            SG = rgbList[1].Skip(i).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(mask_size).ToList();
                            SB = rgbList[2].Skip(i).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(mask_size).ToList();
                            
                            w_temp.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));
                        }

                        w_cand.Add(mask_size/2 + w_temp.IndexOf(w_temp.Min()));
                        w_temp.Clear();
                        mask_offset += mask_skip;
                    }
                    break;

            }

            return Mode(w_cand);
        }

        private double UBDiffSum(List<double[][]> MR, List<double[][]> MG, List<double[][]> MB, List<double[][]> SR, List<double[][]> SG, List<double[][]> SB)
        {
            int mL = MR.Count;
            double cumSum = 0;
            for (int i = 0; i < mL; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    cumSum += Math.Sqrt(sqr(MR[i][7][j] - SR[i][0][j]) + sqr(MG[i][7][j] - SG[i][0][j]) + sqr(MB[i][7][j] - SB[i][0][j]));
                }
            }
            return cumSum / (mL * 8.0);
        }
        #endregion

        #region METHOD 4 - AUTOCORRELATION OF DC VALUES
        private int method4(List<int[][]>[] yccList,int chr)
        {
            List<double> yArray, cbArray, crArray;
            yArray = (chr == 0) ? yccList[0].Select(item => (double)item[0][0]).ToList() : yccList[0].Select(item => (double)item[0][0]).Where((item, index) => index % 2*((chr+1)/2) == 0).ToList();
            cbArray = yccList[1].Select(item => (double)item[0][0]).ToList();
            crArray = yccList[2].Select(item => (double)item[0][0]).ToList();

            List<int> diff = (chr == 3) ? compDiff(yArray).Select(item => item/2).ToList() : compDiff(yArray);
            diff.AddRange(compDiff(cbArray));
            diff.AddRange(compDiff(crArray));

            return Mode(diff);
        }

        private List<int> compDiff(List<double> dArray)
        {
            List<double> acorr = autocorr(dArray);
            List<int> peaks = findPeaks(acorr, 4.0);
            peaks = (peaks.Count < 4) ? findPeaks(acorr, 8.0) : peaks;
            peaks = (peaks.Count < 4) ? findPeaks(acorr, 16.0) : peaks;

            return getDiff(peaks, 4);
        }
        
        #endregion

        #region METHOD 5 - AUTOCORRELATION OF RGB HORIZONTAL BORDER VALUES
        private int method5(List<double[][]>[] rgbList, int chr)
        {
            List<double> subDiff = new List<double>();

            List<double[][]> MR, MG, MB;
            List<double[][]> SR, SG, SB;

            int listSize = rgbList[0].Count;
            int windowSize = Math.Min(Global.maxWidth, listSize);
            int result = 0;
            int minWidth = 40;
            switch(chr)
            {
                case 0:
                    // 4:4:4
                    for (int i = minWidth; i < windowSize; i++)
                    {
                        MR = rgbList[0].Take(listSize-i).ToList();
                        MG = rgbList[1].Take(listSize-i).ToList();
                        MB = rgbList[2].Take(listSize-i).ToList();

                        SR = rgbList[0].Skip(i).ToList();
                        SG = rgbList[1].Skip(i).ToList();
                        SB = rgbList[2].Skip(i).ToList();

                        subDiff.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));

                        MR.Clear();
                        MG.Clear();
                        MB.Clear();
                        SR.Clear();
                        SG.Clear();
                        SB.Clear();
                    }
                    result = subDiff.IndexOf(subDiff.Min()) + minWidth;
                    break;
                case 1:
                    // 4:4:0
                    for (int i = minWidth; i < windowSize; i++)
                    {
                        MR = rgbList[0].Where((item, index) => index % 2 != 0).Take(listSize/2 - i).ToList();
                        MG = rgbList[1].Where((item, index) => index % 2 != 0).Take(listSize/2 - i).ToList();
                        MB = rgbList[2].Where((item, index) => index % 2 != 0).Take(listSize/2 - i).ToList();

                        SR = rgbList[0].Skip(2 * i).Where((item, index) => index % 2 == 0).ToList();
                        SG = rgbList[1].Skip(2 * i).Where((item, index) => index % 2 == 0).ToList();
                        SB = rgbList[2].Skip(2 * i).Where((item, index) => index % 2 == 0).ToList();

                        subDiff.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));

                        MR.Clear();
                        MG.Clear();
                        MB.Clear();
                        SR.Clear();
                        SG.Clear();
                        SB.Clear();
                    }
                    result = subDiff.IndexOf(subDiff.Min()) + minWidth;
                    break;
                case 2:
                    // 4:2:2
                    for (int i = minWidth; i < windowSize; i += 2)
                    {
                        MR = rgbList[0].Take(listSize - i).ToList();
                        MG = rgbList[1].Take(listSize - i).ToList();
                        MB = rgbList[2].Take(listSize - i).ToList();

                        SR = rgbList[0].Skip(i).ToList();
                        SG = rgbList[1].Skip(i).ToList();
                        SB = rgbList[2].Skip(i).ToList();

                        subDiff.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));

                        MR.Clear();
                        MG.Clear();
                        MB.Clear();
                        SR.Clear();
                        SG.Clear();
                        SB.Clear();
                    }
                    result = subDiff.IndexOf(subDiff.Min()) + minWidth / 2;
                    break;
                case 3:
                    //4:2:0
                    for (int i = minWidth; i < windowSize; i += 2)
                    {
                        MR = rgbList[0].Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(listSize / 2 - i).ToList();
                        MG = rgbList[1].Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(listSize / 2 - i).ToList();
                        MB = rgbList[2].Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(listSize / 2 - i).ToList();

                        SR = rgbList[0].Skip(2 * i).Where((item, index) => index % 4 == 0 || index % 4 == 1).ToList();
                        SG = rgbList[1].Skip(2 * i).Where((item, index) => index % 4 == 0 || index % 4 == 1).ToList();
                        SB = rgbList[2].Skip(2 * i).Where((item, index) => index % 4 == 0 || index % 4 == 1).ToList();

                        subDiff.Add(UBDiffSum(MR, MG, MB, SR, SG, SB));

                        MR.Clear();
                        MG.Clear();
                        MB.Clear();
                        SR.Clear();
                        SG.Clear();
                        SB.Clear();
                    }
                    result = subDiff.IndexOf(subDiff.Min()) + minWidth / 2;
                    break;
            }

            return result;
        }
        #endregion

        #region FUSION OF 5 METHODS
        private int finalWidth(List<double[][]>[] rgbList, int w1,int w2,int w3,int w4,int w5)
        {
            List<int> wlist = new List<int> { w1, w2, w3, w4, w5 };
            int mode = Mode<int>(wlist);
            int count=wlist.Select(item => item == mode).ToList().Count;
            if (count > 2)
            {
                return mode;
            }
            else
            {
                // grid diff method
                List<int> modes = new List<int>();
                List<double> gridDiffs = new List<double>();
                while (wlist.Count > 0)
                {
                    gridDiffs.Add(gridDiff(rgbList, mode));
                    modes.Add(mode);
                    wlist.RemoveAll(item => item == mode);
                }
                return modes[gridDiffs.IndexOf(gridDiffs.Min())];
            }
        }

        private double gridDiff(List<double[][]>[] rgbList, int w)
        {
            int mcuSize = (int)Math.Pow(2, (Global.chr + 1) / 2);
            int listSize = w * mcuSize * (rgbList[0].Count / (w * mcuSize));

            List<double[][]> MR = new List<double[][]>();
            List<double[][]> MG = new List<double[][]>();
            List<double[][]> MB = new List<double[][]>();
            List<double[][]> SR = new List<double[][]>();
            List<double[][]> SG = new List<double[][]>();
            List<double[][]> SB = new List<double[][]>();

            double horDiff = 0;
            double verDiff = 0;

            if (Global.chr % 2 == 0)
            {
                MR = rgbList[0].Take(listSize - w * mcuSize).ToList();
                MG = rgbList[1].Take(listSize - w * mcuSize).ToList();
                MB = rgbList[2].Take(listSize - w * mcuSize).ToList();

                SR = rgbList[0].Skip(w * mcuSize).Take(listSize).ToList();
                SG = rgbList[1].Skip(w * mcuSize).Take(listSize).ToList();
                SB = rgbList[2].Skip(w * mcuSize).Take(listSize).ToList();
                
                horDiff = UBDiffSum(MR, MG, MB, SR, SG, SB);

                MR = rgbList[0].Take(listSize).Where((item, index) => index % (w * mcuSize) != w * mcuSize - 1).ToList();
                MG = rgbList[1].Take(listSize).Where((item, index) => index % (w * mcuSize) != w * mcuSize - 1).ToList();
                MB = rgbList[2].Take(listSize).Where((item, index) => index % (w * mcuSize) != w * mcuSize - 1).ToList();

                SR = rgbList[0].Take(listSize).Where((item, index) => index % (w * mcuSize) != 0).ToList();
                SG = rgbList[1].Take(listSize).Where((item, index) => index % (w * mcuSize) != 0).ToList();
                SB = rgbList[2].Take(listSize).Where((item, index) => index % (w * mcuSize) != 0).ToList();

                verDiff = VerticalGridDiff(MR, MG, MB, SR, SG, SB);

            }
            else if(Global.chr==1)
            {
                for (int i = 0; i < listSize / (w * mcuSize) - 1; i++)
                {
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());

                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());

                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());

                    SR.AddRange(rgbList[0].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    SR.AddRange(rgbList[0].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());

                    SG.AddRange(rgbList[1].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    SG.AddRange(rgbList[1].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());

                    SB.AddRange(rgbList[2].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w).ToList());
                    SB.AddRange(rgbList[2].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w).ToList());
                }
                horDiff = UBDiffSum(MR, MG, MB, SR, SG, SB);
                MR.Clear();
                MG.Clear();
                MB.Clear();
                SR.Clear();
                SG.Clear();
                SB.Clear();
                for (int i = 0; i < listSize / (w * mcuSize); i++)
                {
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());

                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());

                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());

                    SR.AddRange(rgbList[0].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    SR.AddRange(rgbList[0].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());

                    SG.AddRange(rgbList[1].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    SG.AddRange(rgbList[1].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());

                    SB.AddRange(rgbList[2].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 != 0).Take(w - 1).ToList());
                    SB.AddRange(rgbList[2].Skip(i * w * mcuSize + mcuSize).Where((item, index) => index % 2 == 0).Take(w - 1).ToList());
                }
                verDiff = VerticalGridDiff(MR, MG, MB, SR, SG, SB);
            }
            else
            {
                for (int i = 0; i < listSize / (w * mcuSize) - 1; i++)
                {
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    SR.AddRange(rgbList[0].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SR.AddRange(rgbList[0].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    SG.AddRange(rgbList[1].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SG.AddRange(rgbList[1].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    SB.AddRange(rgbList[2].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SB.AddRange(rgbList[2].Skip((i + 1) * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());
                }
                horDiff = UBDiffSum(MR, MG, MB, SR, SG, SB);
                MR.Clear();
                MG.Clear();
                MB.Clear();
                SR.Clear();
                SG.Clear();
                SB.Clear();
                for (int i = 0; i < listSize / (w * mcuSize); i++)
                {
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w - 1).ToList());
                    MR.AddRange(rgbList[0].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w - 1).ToList());

                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w - 1).ToList());
                    MG.AddRange(rgbList[1].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w - 1).ToList());

                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w - 1).ToList());
                    MB.AddRange(rgbList[2].Skip(i * w * mcuSize).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w - 1).ToList());

                    SR.AddRange(rgbList[0].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SR.AddRange(rgbList[0].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    SG.AddRange(rgbList[1].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SG.AddRange(rgbList[1].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());

                    SB.AddRange(rgbList[2].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 2 || index % 4 == 3).Take(2 * w).ToList());
                    SB.AddRange(rgbList[2].Skip(i * w * mcuSize + 2).Where((item, index) => index % 4 == 0 || index % 4 == 1).Take(2 * w).ToList());
                }
                verDiff = VerticalGridDiff(MR, MG, MB, SR, SG, SB);
            }

            return Math.Sqrt(sqr(horDiff)+sqr(verDiff));
        }

        private double VerticalGridDiff(List<double[][]> MR, List<double[][]> MG, List<double[][]> MB, List<double[][]> SR, List<double[][]> SG, List<double[][]> SB)
        {
            int mL = Math.Min(MR.Count,SR.Count);
            double cumSum = 0;
            for (int i = 0; i < mL; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    cumSum += Math.Sqrt(sqr(MR[i][j][7] - SR[i][j][0]) + sqr(MG[i][j][7] - SG[i][j][0]) + sqr(MB[i][j][7] - SB[i][j][0]));
                }
            }
            return cumSum / (mL * 8.0);
        }
        #endregion
    }
}
