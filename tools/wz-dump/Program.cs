using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapleClaude.Wz;

// wz-dump — JSON-emitting CLI over the project's own WZ reader (MapleClaude.Wz).
// Designed as the backend for tools/wz-explorer/wz_explorer.py and for the
// wz-explore / wz-gap-audit / wz-gap-implement skills.
//
// All output is a single JSON document on stdout. Errors go to stderr and the
// process exits non-zero. Unicode-safe (BOM-less UTF-8). No interactivity.
//
// Usage:
//   wz-dump tree     <wz> <path>           [--depth N]
//   wz-dump keys     <wz> <path>
//   wz-dump search   <wz> <path> <regex>   [--depth N] [--limit N]
//   wz-dump coverage <wz> <path>           [--depth N] [--limit N]
//   wz-dump detail   <wz> <path>           [--depth N]
//   wz-dump list-images <wz> <directory>
//
//   <wz>     Absolute path to a .wz file, OR a bare name (e.g. "Mob.wz")
//            resolved against $MAPLECLAUDE_WZ_DIR.
//   <path>   Slash-separated node path, e.g. "100100.img/info".
//
// Commands:
//   tree     Recursive JSON dump of the node and its descendants. Use small
//            --depth for big trees (mob.img can be hundreds of nodes).
//   keys     Direct children of <path>: name + type + a tiny summary. Cheap.
//   search   Walks the subtree and returns every node whose *leaf* name matches
//            the regex. Returns the path, type, and a short value preview.
//   coverage Iterates each direct child of <path> (e.g. each mob image inside
//            Mob.wz/) and emits a row { entry, children: [names] }. This is
//            what powers "find every mob with a `jump` node" and the gap audit.
//   detail   Same as `tree` but always enumerates every primitive child,
//            including primitive values (text, numbers, vectors). Use this when
//            you need EVERY field of one specific entry — e.g. for building a
//            typed C# model.
//   list-images
//            Enumerate all .img entries reachable from <directory>. Used by the
//            Python wrapper to drive coverage analyses across an entire WZ.
//
// Important: this CLI never writes anywhere — it's read-only. Safe to call from
// agents/skills without permission prompts.

return Run(args);

static int Run(string[] args)
{
    if (args.Length < 1)
    {
        Stderr("usage: wz-dump <command> <wz> <path> [options]");
        return 64;
    }

    var cmd = args[0];
    try
    {
        return cmd switch
        {
            "tree"        => CmdTree(args),
            "keys"        => CmdKeys(args),
            "search"      => CmdSearch(args),
            "coverage"    => CmdCoverage(args),
            "detail"      => CmdDetail(args),
            "list-images" => CmdListImages(args),
            "--help" or "-h" or "help" => Help(),
            _             => Fail($"unknown command: {cmd}"),
        };
    }
    catch (Exception ex)
    {
        Stderr($"wz-dump: {ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}

static int Help()
{
    Console.WriteLine("commands: tree | keys | search | coverage | detail | list-images");
    Console.WriteLine("see source header for full usage notes.");
    return 0;
}

static int Fail(string msg)
{
    Stderr(msg);
    return 64;
}

static void Stderr(string msg) => Console.Error.WriteLine(msg);

// ── tree ────────────────────────────────────────────────────────────────────
static int CmdTree(string[] args)
{
    if (args.Length < 3) return Fail("usage: wz-dump tree <wz> <path> [--depth N]");
    var maxDepth = ParseIntOpt(args, "--depth", 4);

    using var pkg = OpenWz(args[1]);
    var node = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (node is null) return Fail($"node '{args[2]}' not found");

    WriteJson(NodeToJson(args[2], LeafName(args[2]), node, maxDepth, includePrimitives: true));
    return 0;
}

// ── keys ────────────────────────────────────────────────────────────────────
static int CmdKeys(string[] args)
{
    if (args.Length < 3) return Fail("usage: wz-dump keys <wz> <path>");

    using var pkg = OpenWz(args[1]);
    var node = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (node is null) return Fail($"node '{args[2]}' not found");

    var children = ChildrenOf(node);
    var rows = children.Select(kv => new Dictionary<string, object?>
    {
        ["name"] = kv.Key,
        ["type"] = TypeTag(kv.Value),
        ["summary"] = ValuePreview(kv.Value),
    }).ToList();

    WriteJson(new Dictionary<string, object?>
    {
        ["path"]  = args[2],
        ["count"] = rows.Count,
        ["keys"]  = rows,
    });
    return 0;
}

// ── search ──────────────────────────────────────────────────────────────────
static int CmdSearch(string[] args)
{
    if (args.Length < 4) return Fail("usage: wz-dump search <wz> <path> <regex> [--depth N] [--limit N]");
    var maxDepth = ParseIntOpt(args, "--depth", 8);
    var limit    = ParseIntOpt(args, "--limit", 500);
    var rx = new Regex(args[3], RegexOptions.Compiled | RegexOptions.IgnoreCase);

    using var pkg = OpenWz(args[1]);
    var root = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (root is null) return Fail($"node '{args[2]}' not found");

    var hits = new List<Dictionary<string, object?>>();
    SearchWalk(args[2], LeafName(args[2]), root, 0, maxDepth, rx, hits, limit);

    WriteJson(new Dictionary<string, object?>
    {
        ["path"]  = args[2],
        ["regex"] = args[3],
        ["count"] = hits.Count,
        ["truncated"] = hits.Count >= limit,
        ["hits"]  = hits,
    });
    return 0;
}

static void SearchWalk(string path, string name, object? node, int depth, int maxDepth,
                       Regex rx, List<Dictionary<string, object?>> hits, int limit)
{
    if (hits.Count >= limit) return;

    if (rx.IsMatch(name))
    {
        hits.Add(new Dictionary<string, object?>
        {
            ["path"] = path,
            ["type"] = TypeTag(node),
            ["value"] = ValuePreview(node),
        });
        if (hits.Count >= limit) return;
    }

    if (depth >= maxDepth) return;

    foreach (var (k, v) in ChildrenOf(node))
    {
        SearchWalk(path + "/" + k, k, v, depth + 1, maxDepth, rx, hits, limit);
        if (hits.Count >= limit) return;
    }
}

// ── coverage ────────────────────────────────────────────────────────────────
// For each direct child of <path>, list the names of its children up to --depth
// levels deep. The Python wrapper turns this into a wide matrix
// (entry x optional-node) used by the gap audit.
static int CmdCoverage(string[] args)
{
    if (args.Length < 3) return Fail("usage: wz-dump coverage <wz> <path> [--depth N] [--limit N]");
    var maxDepth = ParseIntOpt(args, "--depth", 2);
    var limit    = ParseIntOpt(args, "--limit", 0); // 0 = no cap

    using var pkg = OpenWz(args[1]);
    var node = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (node is null) return Fail($"node '{args[2]}' not found");

    var rows = new List<Dictionary<string, object?>>();
    var i = 0;
    foreach (var (entryName, entryNode) in ChildrenOf(node))
    {
        if (limit > 0 && i++ >= limit) break;
        var children = new List<string>();
        CollectChildPaths(entryNode, prefix: "", depth: 0, maxDepth, children);
        rows.Add(new Dictionary<string, object?>
        {
            ["entry"]    = entryName,
            ["type"]     = TypeTag(entryNode),
            ["children"] = children,
        });
    }

    WriteJson(new Dictionary<string, object?>
    {
        ["path"]    = args[2],
        ["depth"]   = maxDepth,
        ["entries"] = rows.Count,
        ["rows"]    = rows,
    });
    return 0;
}

static void CollectChildPaths(object? node, string prefix, int depth, int maxDepth, List<string> sink)
{
    if (depth >= maxDepth) return;
    foreach (var (k, v) in ChildrenOf(node))
    {
        var name = string.IsNullOrEmpty(prefix) ? k : prefix + "/" + k;
        sink.Add(name);
        if (depth + 1 < maxDepth)
        {
            CollectChildPaths(v, name, depth + 1, maxDepth, sink);
        }
    }
}

// ── detail ──────────────────────────────────────────────────────────────────
// Same shape as `tree` but always emits primitive values; targeted at "give me
// EVERY field of one specific entry so I can derive a typed model".
static int CmdDetail(string[] args)
{
    if (args.Length < 3) return Fail("usage: wz-dump detail <wz> <path> [--depth N]");
    var maxDepth = ParseIntOpt(args, "--depth", 12);

    using var pkg = OpenWz(args[1]);
    var node = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (node is null) return Fail($"node '{args[2]}' not found");

    WriteJson(NodeToJson(args[2], LeafName(args[2]), node, maxDepth, includePrimitives: true));
    return 0;
}

// ── list-images ─────────────────────────────────────────────────────────────
// Enumerate every .img reachable from <directory>. The Python wrapper uses this
// to drive coverage scans across an entire WZ ("of all mobs, which have jump?").
static int CmdListImages(string[] args)
{
    if (args.Length < 3) return Fail("usage: wz-dump list-images <wz> <directory>");

    using var pkg = OpenWz(args[1]);
    var node = string.IsNullOrEmpty(args[2]) ? (object)pkg.Root : pkg.GetItem(args[2]);
    if (node is null) return Fail($"node '{args[2]}' not found");

    var imgs = new List<string>();
    Walk(args[2], node, imgs);

    WriteJson(new Dictionary<string, object?>
    {
        ["path"]   = args[2],
        ["count"]  = imgs.Count,
        ["images"] = imgs,
    });
    return 0;

    static void Walk(string path, object? n, List<string> sink)
    {
        switch (n)
        {
            case WzDirectory dir:
                foreach (var (k, v) in dir.Items)
                {
                    Walk(path + "/" + k, v, sink);
                }
                break;
            case WzImage:
                sink.Add(path);
                break;
        }
    }
}

// ── helpers ─────────────────────────────────────────────────────────────────
static IEnumerable<KeyValuePair<string, object?>> ChildrenOf(object? node)
{
    switch (node)
    {
        case WzDirectory dir:
            foreach (var (k, v) in dir.Items) yield return new(k, v);
            break;
        case WzImage img:
            foreach (var (k, v) in img.Root.Items) yield return new(k, v);
            break;
        case WzProperty prop:
            foreach (var (k, v) in prop.Items) yield return new(k, v);
            break;
        case WzCanvas canvas:
            foreach (var (k, v) in canvas.Property.Items) yield return new(k, v);
            break;
        case WzUol uol:
            foreach (var c in ChildrenOf(uol.Resolve())) yield return c;
            break;
    }
}

static string TypeTag(object? node) => node switch
{
    WzDirectory => "directory",
    WzImage     => "image",
    WzProperty  => "property",
    WzCanvas    => "canvas",
    WzVector    => "vector",
    WzUol       => "uol",
    WzSound     => "sound",
    short       => "short",
    int         => "int",
    long        => "long",
    float       => "float",
    double      => "double",
    string      => "string",
    null        => "null",
    _           => node.GetType().Name.ToLowerInvariant(),
};

static object? ValuePreview(object? node)
{
    return node switch
    {
        WzCanvas c   => new Dictionary<string, object?>
        {
            ["w"] = c.Width, ["h"] = c.Height, ["format"] = c.Format,
            ["origin"] = c.Property.Get("origin") is WzVector v ? new[] { v.X, v.Y } : null,
        },
        WzVector v   => new[] { v.X, v.Y },
        WzUol u      => "->" + (u.Target ?? "?"),
        WzSound      => null,
        WzDirectory d => new Dictionary<string, object?> { ["children"] = d.Items.Count },
        WzImage i     => new Dictionary<string, object?> { ["children"] = i.Root.Items.Count },
        WzProperty p  => new Dictionary<string, object?> { ["children"] = p.Items.Count },
        short s  => (int)s,
        int n    => n,
        long l   => l,
        float f  => (double)f,
        double d => d,
        string s => Truncate(s, 240),
        null     => null,
        _        => node.ToString(),
    };
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

static Dictionary<string, object?> NodeToJson(string path, string name, object? node,
                                              int maxDepth, bool includePrimitives)
{
    var row = new Dictionary<string, object?>
    {
        ["path"]    = path,
        ["name"]    = name,
        ["type"]    = TypeTag(node),
        ["summary"] = ValuePreview(node),
    };

    if (node is WzVector v)
    {
        row["x"] = v.X; row["y"] = v.Y;
        return row;
    }
    if (node is WzUol u)
    {
        row["target"] = u.Target;
        return row;
    }

    var children = ChildrenOf(node).ToList();
    if (children.Count == 0 || maxDepth <= 0)
    {
        if (includePrimitives && IsPrimitive(node))
        {
            row["value"] = ValuePreview(node);
        }
        return row;
    }

    var kids = new List<Dictionary<string, object?>>();
    foreach (var (k, vv) in children)
    {
        kids.Add(NodeToJson(path + "/" + k, k, vv, maxDepth - 1, includePrimitives));
    }
    row["children"] = kids;
    return row;
}

static bool IsPrimitive(object? n) =>
    n is short or int or long or float or double or string or WzVector or WzUol;

static string LeafName(string path)
{
    var i = path.LastIndexOf('/');
    return i < 0 ? path : path[(i + 1)..];
}

static int ParseIntOpt(string[] args, string flag, int defaultValue)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == flag && int.TryParse(args[i + 1], NumberStyles.Integer,
                                            CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }
    }
    return defaultValue;
}

static WzPackage OpenWz(string arg)
{
    if (File.Exists(arg)) return WzPackage.Open(arg);
    var dir = Environment.GetEnvironmentVariable("MAPLECLAUDE_WZ_DIR");
    if (!string.IsNullOrEmpty(dir))
    {
        var combined = Path.Combine(dir, arg);
        if (File.Exists(combined)) return WzPackage.Open(combined);
    }
    throw new FileNotFoundException(
        $"could not resolve WZ '{arg}' — pass an absolute path or set $MAPLECLAUDE_WZ_DIR");
}

static void WriteJson(object value)
{
    var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
    // Force LF-only and BOM-free so Python json.loads is happy.
    Console.Out.NewLine = "\n";
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    Console.Out.Write(json);
    Console.Out.Write('\n');
}
