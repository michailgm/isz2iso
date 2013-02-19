using System;
using System.Diagnostics;
using System.IO;
using DiskImage;

namespace isz2iso
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Usage();
                return;
            }

            Console.WriteLine(string.Format(@"converting... ""{0}"" ==> ""{1}""", args[0], args[1]));
            Isz2IsoConvert(args[0], args[1]);
        }

        static void Usage()
        {
            string exeFileName = Process.GetCurrentProcess().MainModule.FileName;
            Console.WriteLine(string.Format("Usage: {0} src_file.isz dst_file.iso", Path.GetFileNameWithoutExtension(exeFileName)));
        }

        static void Isz2IsoConvert(string sourceFileName, string destFileName)
        {
            if (!File.Exists(sourceFileName))
            {
                Console.WriteLine(string.Format(@"Source file ""{0}"" not exists.", sourceFileName));
                return;
            }

            using (FileStream iszFile = File.OpenRead(sourceFileName))
            using (FileStream isoFile = File.Create(destFileName))
            using (IszInputStream iszDecompressStream = new IszInputStream(iszFile))
            {
                iszDecompressStream.CopyTo(isoFile);
            }
        }
    }
}
