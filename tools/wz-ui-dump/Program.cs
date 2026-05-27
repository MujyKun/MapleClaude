using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using MapleClaude.Wz;

// wz-ui-dump — prints a WZ node subtree with each canvas's dimensions and
// origin. This is the asset-side "origin detector": it shows exactly which UI
// components exist under a path and where their anchor (origin) sits, which —
// combined with the placement coordinates the client code uses — determines the
// final on-screen pixel position of every widget.
//
// Usage:
//   wz-ui-dump <wzFileOrName> <nodePath> [--depth N] [--canvas-only]
//
//   <wzFileOrName>  Absolute path to a .wz file, OR a bare name (e.g. "UI.wz")
//                   resolved against the MAPLECLAUDE_WZ_DIR environment variable.
//   <nodePath>      Slash-separated node path, e.g. "Login.img/WorldSelect".
//   --depth N       Max recursion depth (default 6).
//   --canvas-only   Only print canvas nodes (skip pure-container chatter).
//
// Examples:
//   wz-ui-dump UI.wz Login.img/WorldSelect
//   wz-ui-dump UI.wz Login.img/Common --depth 3

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "usage: wz-ui-dump <wzFileOrName> <nodePath> [--depth N] [--canvas-only]");
    return 1;
}

var wzArg = args[0];
var nodePath = args[1];
var maxDepth = 6;
var canvasOnly = false;
string? pngPath = null;

for (var i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--depth" when i + 1 < args.Length && int.TryParse(args[i + 1], out var d):
            maxDepth = d;
            i++;
            break;
        case "--canvas-only":
            canvasOnly = true;
            break;
        case "--png" when i + 1 < args.Length:
            pngPath = args[i + 1];
            i++;
            break;
        default:
            Console.Error.WriteLine($"unknown argument: {args[i]}");
            return 1;
    }
}

var wzPath = ResolveWzPath(wzArg);
if (wzPath is null)
{
    Console.Error.WriteLine(
        $"could not resolve WZ file '{wzArg}'. Pass an absolute path, or set " +
        "MAPLECLAUDE_WZ_DIR to the directory holding the .wz files.");
    return 1;
}

using var pkg = WzPackage.Open(wzPath);
var root = pkg.GetItem(nodePath);
if (root is null)
{
    Console.Error.WriteLine($"node '{nodePath}' not found in {Path.GetFileName(wzPath)}");
    return 1;
}

if (pngPath is not null)
{
    var canvas = root as WzCanvas
        ?? (root as WzProperty)?.Get("0") as WzCanvas
        ?? ((root as WzProperty)?.Get("0") as WzProperty)?.Get("0") as WzCanvas;
    if (canvas is null)
    {
        Console.Error.WriteLine($"node '{nodePath}' is not a canvas (and has no /0 canvas child)");
        return 1;
    }
    return ExportPng(canvas, pngPath);
}

Console.WriteLine($"# {Path.GetFileName(wzPath)} :: {nodePath}");
Dump(LeafName(nodePath), root, 0);
return 0;

static int ExportPng(WzCanvas canvas, string path)
{
    ReadOnlySpan<byte> rgba;
    try
    {
        rgba = canvas.DecodeBgra(); // byte order R,G,B,A per WzCanvas.DecodeBgra
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"decode failed ({canvas.Width}x{canvas.Height}, format {canvas.Format}): {ex.Message}");
        return 1;
    }
    WritePng(path, canvas.Width, canvas.Height, rgba);
    Console.WriteLine($"wrote {path} ({canvas.Width}x{canvas.Height}, format {canvas.Format})");
    return 0;
}

static void WritePng(string path, int w, int h, ReadOnlySpan<byte> rgba)
{
    using var fs = File.Create(path);
    fs.Write([137, 80, 78, 71, 13, 10, 26, 10]); // PNG signature

    var ihdr = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), w);
    BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
    ihdr[8] = 8;  // bit depth
    ihdr[9] = 6;  // colour type: RGBA
    WriteChunk(fs, "IHDR", ihdr);

    // Filter-byte 0 per scanline, RGBA rows (DecodeBgra already gives R,G,B,A).
    var raw = new byte[h * (1 + w * 4)];
    for (var y = 0; y < h; y++)
    {
        var ro = y * (1 + w * 4);
        raw[ro] = 0;
        rgba.Slice(y * w * 4, w * 4).CopyTo(raw.AsSpan(ro + 1));
    }
    using var ms = new MemoryStream();
    using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
    {
        z.Write(raw);
    }
    WriteChunk(fs, "IDAT", ms.ToArray());
    WriteChunk(fs, "IEND", []);
}

static void WriteChunk(Stream s, string type, byte[] data)
{
    Span<byte> len = stackalloc byte[4];
    BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
    s.Write(len);
    var typeBytes = Encoding.ASCII.GetBytes(type);
    s.Write(typeBytes);
    s.Write(data);
    var crc = 0xFFFFFFFFu;
    crc = Crc32(crc, typeBytes);
    crc = Crc32(crc, data);
    Span<byte> crcb = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(crcb, crc ^ 0xFFFFFFFFu);
    s.Write(crcb);
}

static uint Crc32(uint crc, byte[] d)
{
    foreach (var b in d)
    {
        crc ^= b;
        for (var i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
    }
    return crc;
}

static string LeafName(string path)
{
    var slash = path.LastIndexOf('/');
    return slash < 0 ? path : path[(slash + 1)..];
}

string? ResolveWzPath(string arg)
{
    if (File.Exists(arg))
    {
        return arg;
    }
    var dir = Environment.GetEnvironmentVariable("MAPLECLAUDE_WZ_DIR");
    if (!string.IsNullOrEmpty(dir))
    {
        var combined = Path.Combine(dir, arg);
        if (File.Exists(combined))
        {
            return combined;
        }
    }
    return null;
}

void Dump(string name, object? node, int depth)
{
    if (depth > maxDepth)
    {
        return;
    }

    var indent = new string(' ', depth * 2);
    switch (node)
    {
        case WzCanvas canvas:
        {
            var origin = canvas.Property.Get("origin") is WzVector v ? v : WzVector.Zero;
            Console.WriteLine($"{indent}{name}  [Canvas {canvas.Width}x{canvas.Height} origin={origin}]");
            DumpChildren(canvas.Property.Items, depth + 1, skipOrigin: true);
            break;
        }

        case WzImage image:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name}  (Image)");
            }
            DumpChildren(image.Root.Items, depth + 1, skipOrigin: false);
            break;

        case WzProperty prop:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name}  (Property)");
            }
            DumpChildren(prop.Items, depth + 1, skipOrigin: false);
            break;

        case WzUol uol:
        {
            var resolved = uol.Resolve();
            Console.WriteLine($"{indent}{name}  (UOL -> {(resolved is null ? "?" : resolved.GetType().Name)})");
            if (resolved is not null && depth < maxDepth)
            {
                Dump(name + "*", resolved, depth + 1);
            }
            break;
        }

        case WzVector vec:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name} = {vec}");
            }
            break;

        case WzSound:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name}  (Sound)");
            }
            break;

        case null:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name} = (null)");
            }
            break;

        default:
            if (!canvasOnly)
            {
                Console.WriteLine($"{indent}{name} = {node} ({node.GetType().Name})");
            }
            break;
    }
}

void DumpChildren(IReadOnlyDictionary<string, object?> items, int depth, bool skipOrigin)
{
    foreach (var (key, value) in items)
    {
        if (skipOrigin && key == "origin")
        {
            continue;
        }
        Dump(key, value, depth);
    }
}
