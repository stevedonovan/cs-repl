csc -nologo  -optimize -out:csr.exe csr.cs prepro.cs interpreter.cs
csc -nologo  -optimize -t:winexe -r:System.Windows.Forms.dll -r:System.Drawing.dll  -out:csrgui.exe csrgui.cs console.cs prepro.cs interpreter.cs
