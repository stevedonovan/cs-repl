// CS-REPL: A simple C# REPL
// Copyright, Steve Donovan 2005-2013
// Use freely, but please acknowledge! (MIT license)
using System;
using System.IO;
using System.Windows.Forms;

public class RunCsi{
    const string caption = "CS-REPL Simple C# Interpreter vs 0.8",
                       prompt = "> ";

    static Interpreter interp = null;

    static void ProcessLine(string line) {
        interp.ProcessLine(line);
    }

    public static void Main(string[] args) {
        GuiConsoleForm form = new GuiConsoleForm(caption,prompt,new StringHandler(ProcessLine));
        GuiConsole console = new GuiConsole(form);
        console.Write(caption+"\n"+prompt);
        Interpreter.Console = console;
        interp = new Interpreter();
        string defs = args.Length > 0 ? args[0] :  interp.DefaultIncludeFile();
        interp.ReadIncludeFile(defs);
        interp.SetValue("form",form);
        interp.SetValue("text",form.TextBox);
        Application.Run(form);
    }
}

class GuiConsole : IConsole {
    GuiConsoleForm form;

    public GuiConsole(GuiConsoleForm f) {
        form = f;
    }

    public string ReadLine() {
        return "";
    }

    public void Write(string s) {
        form.Write(s);
    }
}
