using System;
using System.IO;
class HarmlessCompanion
{
    static int Main(string[] args)
    {
        File.WriteAllLines(Environment.GetEnvironmentVariable("NFS_TEST_OUTPUT"), args);
        return 0;
    }
}
