// Tiny diagnostic: writes a KGP APC query to stdout and exits.
// Run inside WpfTerm to test if APC passes through ConPTY.
using System.Text;
Console.OutputEncoding = Encoding.UTF8;

// ESC _ G a=q,i=99,s=1,v=1,f=32; AAAA ESC \
// This is a KGP query (action=query) with 1x1 RGBA pixel
var apc = "\x1b_Ga=q,i=99,s=1,v=1,f=32;AAAA\x1b\\";
Console.Write("Sending KGP APC probe...");
Console.Out.Flush();

// Write raw bytes to stdout  
using var stdout = Console.OpenStandardOutput();
stdout.Write(Encoding.UTF8.GetBytes(apc));
stdout.Flush();

Console.WriteLine(" done. Check title bar for token count.");
