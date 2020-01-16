using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegRecovery
{

    class KMeans
    {
        /*static void Main(string[] args)
        {
            Console.WriteLine("\nBegin k-means clustering demo\n");
            double[] rawData = { 65.0, 73.0, 59.0, 61.0, 75.0, 67.0, 68.0, 70.0, 62.0, 66.0, 77.0, 75.0, 74.0, 70.0, 61.0, 58.0, 66.0, 59.0, 68.0, 61.0 };

            Console.WriteLine("Raw unclustered data:\n");
            Console.WriteLine("    Height Weight");
            Console.WriteLine("-------------------");
            ShowData(rawData, 1, true, true);

            int numClusters = 2;
            Console.WriteLine("\nSetting numClusters to " + numClusters);

            int[] clustering = Cluster(rawData, numClusters); // this is it

            Console.WriteLine("\nK-means clustering complete\n");

            Console.WriteLine("Final clustering in internal form:\n");
            ShowVector(clustering, true);

            Console.WriteLine("Raw data by cluster:\n");
            ShowClustered(rawData, clustering, numClusters, 1);

            Console.WriteLine("\nEnd k-means clustering demo\n");
            Console.ReadLine();

        } // Main*/

        // ============================================================================

        public static int[] Cluster(List<double> rawData, int numClusters)
        {
            double[] data = Normalized(rawData); // so large values don't dominate

            bool changed = true; // was there a change in at least one cluster assignment?
            bool success = true; // were all means able to be computed? (no zero-count clusters)

            int[] clustering = InitClustering(data.Length, numClusters, 0); // semi-random initialization
            double[] means = new double[numClusters];// small convenience

            int maxCount = data.Length * 10; // sanity check
            int ct = 0;
            while (changed == true && success == true && ct < maxCount)
            {
                ++ct; // k-means typically converges very quickly
                success = UpdateMeans(data, clustering, means); // compute new cluster means if possible. no effect if fail
                changed = UpdateClustering(data, clustering, means); // (re)assign tuples to clusters. no effect if fail
            }
            return clustering;
        }

        private static double[] Normalized(List<double> rawData)
        {
            double[] result = new double[rawData.Count];
            double mean = rawData.Average();
            double sd = rawData.Average(i => (i - mean) * (i - mean));
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (rawData[i] - mean) / sd;
            }
            return result;
        }

        private static int[] InitClustering(int numTuples, int numClusters, int randomSeed)
        {
            Random random = new Random(randomSeed);
            int[] clustering = new int[numTuples];
            for (int i = 0; i < numClusters; ++i) // make sure each cluster has at least one tuple
                clustering[i] = i;
            for (int i = numClusters; i < clustering.Length; ++i)
                clustering[i] = random.Next(0, numClusters); // other assignments random
            return clustering;
        }


        private static bool UpdateMeans(double[] data, int[] clustering, double[] means)
        {
            // returns false if there is a cluster that has no tuples assigned to it
            // parameter means[][] is really a ref parameter

            // check existing cluster counts
            // can omit this check if InitClustering and UpdateClustering
            // both guarantee at least one tuple in each cluster (usually true)
            int numClusters = means.Length;
            int[] clusterCounts = new int[numClusters];
            for (int i = 0; i < data.Length; ++i)
            {
                int cluster = clustering[i];
                ++clusterCounts[cluster];
            }

            for (int k = 0; k < numClusters; ++k)
                if (clusterCounts[k] == 0)
                    return false; // bad clustering. no change to means[][]

            // update, zero-out means so it can be used as scratch matrix 
            for (int k = 0; k < means.Length; ++k)
                means[k] = 0.0;

            for (int i = 0; i < data.Length; ++i)
            {
                means[clustering[i]] += data[i];
            }

            for (int k = 0; k < means.Length; ++k)
                means[k] /= clusterCounts[k];
            return true;
        }

        private static bool UpdateClustering(double[] data, int[] clustering, double[] means)
        {
            int numClusters = means.Length;
            bool changed = false;

            int[] newClustering = new int[clustering.Length]; // proposed result
            Array.Copy(clustering, newClustering, clustering.Length);

            double[] distances = new double[numClusters]; // distances from curr tuple to each mean

            for (int i = 0; i < data.Length; ++i) // walk thru each tuple
            {
                for (int k = 0; k < numClusters; ++k)
                    distances[k] =Math.Abs( data[i]-means[k] );//compute distances from curr tuple to all k means

                int newClusterID = Array.IndexOf(distances,distances.Min()); // find closest mean ID
                if (newClusterID != newClustering[i])
                {
                    changed = true;
                    newClustering[i] = newClusterID; // update
                }
            }

            if (changed == false)
                return false; // no change so bail and don't update clustering[][]

            // check proposed clustering[] cluster counts
            int[] clusterCounts = new int[numClusters];
            for (int i = 0; i < data.Length; ++i)
            {
                int cluster = newClustering[i];
                ++clusterCounts[cluster];
            }

            for (int k = 0; k < numClusters; ++k)
                if (clusterCounts[k] == 0)
                    return false; // bad clustering. no change to clustering[][]

            Array.Copy(newClustering, clustering, newClustering.Length); // update
            return true; // good clustering and at least one change
        }

        // ============================================================================

        // misc display helpers for demo

        //static void ShowData(double[] data, int decimals, bool indices, bool newLine)
        //{
        //    for (int i = 0; i < data.Length; ++i)
        //    {
        //        if (indices) Console.Write(i.ToString().PadLeft(3) + " ");
        //        if (data[i] >= 0.0) Console.Write(" ");
        //        Console.Write(data[i].ToString("F" + decimals) + " ");
        //        Console.WriteLine("");
        //    }
        //    if (newLine) Console.WriteLine("");
        //} // ShowData

        //static void ShowVector(int[] vector, bool newLine)
        //{
        //    for (int i = 0; i < vector.Length; ++i)
        //        Console.Write(vector[i] + " ");
        //    if (newLine) Console.WriteLine("\n");
        //}

        //static void ShowClustered(double[] data, int[] clustering, int numClusters, int decimals)
        //{
        //    for (int k = 0; k < numClusters; ++k)
        //    {
        //        Console.WriteLine("===================");
        //        for (int i = 0; i < data.Length; ++i)
        //        {
        //            int clusterID = clustering[i];
        //            if (clusterID != k) continue;
        //            Console.Write(i.ToString().PadLeft(3) + " ");
        //            if (data[i] >= 0.0) Console.Write(" ");
        //            Console.Write(data[i].ToString("F" + decimals) + " ");
        //            Console.WriteLine("");
        //        }
        //        Console.WriteLine("===================");
        //    } // k
        //}
    } // Program


}
