# CS-REPL, an interactive C# Environment

[This is a program which I did way back in 2005, and was the basis of [a CodeProject article](http://www.codeproject.com/Articles/10212/CSI-A-Simple-C-Interpreter). Since then, David Anson has released his [CSI](http://blogs.msdn.com/b/delay/archive/2010/01/07/the-source-code-is-still-the-executable-updated-csi-a-c-interpreter-with-source-and-tests-for-net-4.aspx) which _is_ an actual interpreter. So I've done the noble thing and renamed this project]

It is very useful to have a way of quickly testing small pieces of code. This is
particularly useful if you are exploring a new system or library for the first
time. Interactive interpreters allow for [conversational programming](http://quepublishing.com/articles/article.asp?p=25942)  and it's a
popular feature of languages such as Python. Visual Basic will allow you to execute code
in the Immediate Window, but there are limits to what can be done in Visual Studio. For
instance, you cannot declare and create new objects.

Traditionally, compiled languages such as C# are not usually used in this
interactive way, but the .NET framework makes it straightforward to compile code
 a line at a time, using the System.CodeDom.Compiler and Microsoft.CSharp
namespaces. There have been several programs for using C# as a [scripting language](http://www.codeproject.com/dotnet/DotNetScript.asp)
 that avoid a separate compile step. The argument there is that since C#
compilation is so fast for small programs, one doesn't need the full machinery of
Visual Studio to build and manage them. cs-repl is a
reimplementation of [C#Shell](http://csshell.sourceforge.net/) which was designed for the Mono framework. cs-repl is
faster, because it does not actually have to spawn the full compiler, and uses a
technique for creating statically typed session variables. An interactive example will
make this clearer:

```csharp
d:\> csr
CS-REPL Simple C# Interpreter
# $s = "Hello, World!"
# Print($s.Substring(0,5),$s.GetType())
Hello  System.String
# $l = $s.Split(null)
# foreach(string ss in $l) Print(ss)
Hello,
World!
```

In the first line, we assign a string to a session variable `$s`; note that a
semicolon is automatically appended to each line. The function `Print` is available
, which takes a variable number of arguments. It's easier to type than
`Console.WriteLine` and will also work in a GUI console session. Statements such as
`foreach` and any other legal C# code can be evaluated.

## Implementation

cs-repl relies on .NET's own code compilation libraries, so it doesn't actually need
 to parse and interpret C# code - technically it is an incremental compiler. So
the technical problem is how to keep a common environment active between each
separately compiled line. For each line typed, cs-repl generates and compiles a new
assembly that looks like this:


```csharp
 (any using namespaces)
 class CsiChunk : CodeChunk {
     public override void Go (Hashtable V) {
         (code goes here)
     }
  }
```

There are some commands to control the compilation context. For instance, `/n
System.IO` will insert a `using System.IO;` in the generated code; `/r
System.Drawing.dll` will add a reference to that assembly. Any cs-repl commands can be
 put in a session include file (csrgui.csr, csr.csr for the GUI and console
versions respectively; I've included some examples of these with the source and
binaries).

Session variables are replaced with global lookup table references. The line `$s =
 "Hello, World!"` becomes `V["s"] = "Hello, World!";`. In the same way (but with a
key difference) `$s.Substring(0,5)` becomes `((System.String)V["s
"]).Substring(0,5)`. Any references other than assignments are cast to the actual type of
the variable. I'm relying on a very cool C# feature called autoboxing where any value
type is automatically converted into an object on heap. This allows any value (such as
numbers) to be boxed as an object and put into an object container such as a `HashTable`,
and later casting to the correct type will unbox the value.

The actual compilation is the most straightforward part of cs-repl, and is quite
standard. The compiled assembly is loaded by dynamically instantiating the
`CsiChunk` class. cs-repl doesn't have such a class, but both cs-repl and the assembly know
about CodeChunk. So I can cast the object to `CodeChunk` and call the overridden `Go`
method, passing it the global lookup table.

```csharp
   public static void Instantiate(Assembly a, Hashtable table) {
        try {
            CodeChunk chunk = (CodeChunk)a.CreateInstance("CsiChunk");
            chunk.Go(table);
        }  catch(Exception ex) {
            Print(ex.GetType() + " was thrown: " + ex.Message);
        }
    }
```

Session variables would be fairly useless unless they cast to their correct type.
 cs-repl has to massage code so that `$var` is replaced by `V["var"]`. If it is an
assignment then the value is not cast. Otherwise, find the type of the object `V[
"var"]` and use that. It's important to find a publicly accessible type, because
the actual runtime type may be an implementation class which isn't available to
 us. For instance, `Type.GetMethods` returns an array of `RuntimeMethodInfo`, which
is derived from `MethodInfo`. So if the type is a class, we look for a public base
class.

```csharp
    Type GetPublicRuntimeType(object symVal) {
        Type symType = null;
        if (symVal != null) {
            symType = symVal.GetType();
            while (! symType.IsPublic)
                symType = symType.BaseType;
        }
        return symType;
    }
```

It's important to understand this scheme, because then certain basic
limitations of cs-repl become clear. First, there must be actual assignment, so
the object can't be created by a reference parameter. Multiple assignments on a
line is allowable, but session variables don't have a definite type until the next
line:

```csharp
# $x = 1.0; $y = 2.0; $z = 3.0
# Print($x+$y*$z)
7
# $a = 10; $b = 20; Print($a + $b);
Compiling string: 'V["a"] = 10; V["b"] = 20; Print(V["a"] + V["b"]);'
Operator '+' cannot be applied to operands of type 'object' and 'object'
```

You can extend a 'line' over several lines of input, if it uses braces. cs-repl isn
't psychic, so it's necessary to put the brace on the first line so braces can be
 counted properly. Here semicolons are essential and $sum must have been
previously declared. Any declarations inside such a block must be explicit.

```csharp
# foreach(Item item in $items) {
      $sum += item.Size;
      Print($sum,item.Size);
}
```

In short, session variables can only be used in subsequent lines, and multiline
blocks really only count as single lines of compilation.
Macros and Functions

One thing I learnt from the [UnderC Project](https://github.com/stevedonovan/underc) is that a C-style macro preprocessor
is very useful in interactive work. Its obvious use is to make long identifiers
and tricky
constructs easier to type:

```csharp
# #def FOR(i,n) for(int i =0; i < (n); i++)
# FOR(k,5) Print(k,k*k)
0 0
1 1
2 4
3 9
4 16
# #def wl Console.WriteLine
# wl("{0} == {1}",10,10)
10 == 10
```

I'm not suggesting that this is a good style for normal C#! Experience has
shown that macro processors lead to trouble in production code, since people
tend to construct a private language and it makes debugging much harder. But
in interactive work, you can write informal code, just as with English. I
would hesitate to use "ain't" in written articles, but it's fine in conversation.

Macros may be used to define new commands (which begin with '/').

```csharp
# #def P(x) Print
#/P 2*6 + 1
13
```

The main reason that cs-repl has a preprocessor is that it makes defining and using
functions more convenient. You can define functions and use them thereafter as if
 they were part of cs-repl.

```csharp
# double sqr(double x) { return x*x; }
# Print(sqr(10))
100
# Print(Math.Sqrt(sqr(10)))
10
# void dump(int[] arr) {
#  FOR(i,arr.Length)
#   Print(i,arr[i]);
# }
# dump(new int[] { 1, 7, 3, 4 })
0 1
1 7
2 3
3 4
```

cs-repl knows that a function is being defined by merely looking for a pattern where
two identifiers start the line followed by an argument list and an open brace.
The definition for sqr above results in this assembly being compiled as Csi1.dll.

```csharp
public class Csi1 : CsiFunctionContext {
  public double _sqr(double x) { return x*x; }
}
```

A macro 'sqr' is then defined to be 'Csi1._sqr', which makes it possible to use
the function without knowing which assembly it lives in. Subsequent functions
will be in Csi2.dll, and so on.
Graphical Console

cs-repl can be built as a console program, or as a Windows Forms application. Not
only does this provide a nicer environment, but it allows GUI code to be tested.
For example, although you can create and show a window from the console version,
it cannot do anything interesting because there is no event loop. There are some
interesting issues about building graphical consoles in .NET which were not
clearly documented, so I'll describe how to do it here.

A RichTextBox is the obvious control, but there is some necessary work to
intercept the ENTER key. The solution is to derive a custom text box and override
 IsInputKey:

```csharp
    protected override bool IsInputKey(Keys keyData) {
        if (keyData == Keys.Enter) {
            int lineNo = GetLineFromCharIndex(SelectionStart);
            if (lineNo < Lines.Length) {
                string line = Lines[lineNo];
                parent.DelayedExecute(line);
            }
       }
       return base.IsInputKey(keyData);
   }
```

Text boxes have a useful property called Lines which acts like an indexable
collection of all the lines in the control. It is straightforward to get the line
 which the user has just entered. I have found, however, that WordWrap must be
switched off for this scheme to work properly, and it is important not to try
modifying the control from within this function. So the current line is passed to
 a function which starts a timer, and a short while later, the prompt can be
written and the line evaluated by the interpreter:

```csharp
    public void DelayedExecute(string line) {
        currentLine = line.Substring(prompt.Length);
        timer.Start();
    }

    void Execute(object sender,EventArgs e) {
        timer.Stop();
        stringHandler(currentLine);
        Write(prompt);
    }
```

## Some ideas for using cs-repl

It's possible to create useful GUI extensions to cs-repl just using its
facilities. For example, the default session file csigui.cs-repl contains the
following code which creates a form and fills it with a PropertyGrid control.
A macro I is defined which sets the SelectedObject property of the grid and
makes the form accessible. For efficiency reasons, I've packed everything into
two lines.

```csharp
$pf=new Form();$pg=new PropertyGrid()
$pg.SelectedObject =
  $pf;$pg.Dock=DockStyle.Fill;$pf.Controls.Add($pg);$pf.Text="Properties";$
pf.Show();
#def I(x) $pg.SelectedObject=x; $pf.BringToFront()
```

The session variables $form and $text are always available in the GUI build. To
inspect the properties of the text box, simply say /I $text.

cs-repl exports a function called MInfo. This uses introspection to either list the
methods of a class or detailed information about a particular method. I've
defined two macros which make this easier to use, which are defined in the sample
session include files. This is useful when exploring an assembly for the first time.

```csharp
# #def M(klass) MInfo(typeof(klass),null)
# #def MI(method) MInfo(null, #method)
# /M string
ToString GetTypeCode Clone CompareTo GetHashCode
Equals ToString Join Equals CopyTo
ToCharArray Split Substring Trim TrimStart
TrimEnd Compare CompareTo CompareOrdinal EndsWith
IndexOf IndexOfAny IndexOf LastIndexOf LastIndexOfAny
LastIndexOf PadLeft PadRight StartsWith ToLower
ToUpper Trim Insert Replace Remove
Format Copy Concat Intern IsInterned
GetEnumerator
# /MI Split
String[] Split(Char[])
String[] Split(Char[], Int32)
# /MI Remove
String Remove(Int32, Int32)
# /P "hello dolly".Remove(2,3)
he dolly
```

You can of course load your own assemblies with /r. For instance, say I had a
robot.dll which controlled a robot (what else?). By clever use of macros, it
becomes possible to test your robot interactively. I can create a
RobotController object, and mosey the device around using simple commands.

```csharp
/r robot.dll
# $robot = new RobotController()
# /P $robot
robbie
# $robot.TurnLeft()
# $robot.Move(10)
# #def TL $robot.TurnLeft()
# #def MV(x) $robot.Move(x)
# TL
# MV(10)
# /MV 10
```

Unit testing is very much part of the software buzz these days, and interactive
programming can help in initial exploration. This style has proved very
productive working with hardware (consider the history of the FORTH programming
language.)

Another interesting application of cs-repl is as an embedded console that exposes the
 innards of your application to interactive testing. An embedded cs-repl prompt
allows you to 'crawl inside' your program and test components in their working
environment. This applies to cs-repl itself - the main Interpreter class is available
 from within an interactive session, and the session variable $interpreter has
already been set.

```csharp
> /P Assembly.GetAssembly(typeof(Interpreter))
csigui, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
> /M Interpreter
ReadIncludeFile SetValue ProcessLine AddNamespace AddReference
> /P $interpreter
Interpreter
> $interpreter.SetValue("alice","here we go")
> Print($alice,'*',$alice.Remove(0,2))
here we go * re we go
```

cs-repl can be linked in as a small 15 K DLL referenced by your program, which can
access all your public classes, provided the assemblies are referenced. If
nothing else, it can be used as an intelligent trace monitor window, if you
output your traces through Debug.Trace, which is defined below. In the console
window, you can now type Debug.Tracing = true and so switch on tracing selectively.

```csharp
public delegate void ObjectTracer(object o);

public class Debug {
    static public bool Tracing = false;
    static public ObjectTracer TraceHook = null;

    public static void Trace(params object[] objs) {
        if (Tracing)
            Utils.Printl(objs);
    }

    public static void TraceObject(object o) {
        if (TraceHook != null)
            TraceHook(o);
    }
}
```

Calls to Debug.TraceObject can be customized (by default, they do nothing).
Assuming I've liberally put such calls throughout my code, it's now possible to
execute your own arbitrary code dynamically. Here I'm only interested in looking at
trace calls for any LineAdaptor object. This technique would be useful in displaying
objects that only satisfy some arbitrary criteria, rather than having to wade through
thousands of trace output lines.

```csharp
> void dump(object o) { if (o.GetType() == typeof(LineAdaptor)) Print(o); }
> Debug.TraceHook = new ObjectTracer(dump)
.... exercise your program, looking at all LineAdaptor objects...
> Debug.TraceHook = null
```

## Criticisms and Future Possibilities

The approach used in cs-repl is potentially wasteful of system resources, because
every little assembly created remains loaded. In a long session, this might
eventually be a problem, but the leak would be quite slow due to the small size
of typical compiled lines.

A good question (which I'm expecting from the .NET wizards) is 'why not use
Application Domains?'. Surely, running code in a separate AppDomain provides
advantages? This is true, but not so much in this case. Since we keep
references to all objects generated, it isn't possible to close a separate
AppDomain without resetting the whole session and losing all created variables.
So it seems easier to use the default include file (cs-repl.cs-repl or csigui.cs-repl)
sensibly and just restart cs-repl. Of course, I may have missed something subtle
here. The main reason is that it would make cs-repl a harder program to understand. (
The actual core is currently less than three hundred lines.)

There are several minor issues which occurred to me. It would be cool if the
graphical console used different colours for input and output, as I did for the
UnderC project. People may like an inspectable list of currently created session
 variables, although it would be more interesting to supply the necessary general
 hooks in cs-repl so that a variable list could be generated using cs-repl scripts (like
the Property Inspector window). Currently there can be only one instance of
Interpreter in an application, and it may be useful to relax that restriction.

I've found cs-repl to be an exciting program to play with, and it's become an
invaluable part of my programming tool chest. Embedded .NET-aware interpreters
make new debugging tactics possible, and allow you to test your code
interactively.
