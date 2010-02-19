
shader.exe: *.cs
	gmcs *.cs /unsafe /r:Mono.Simd /out:shader.exe

run: shader.exe
	mono shader.exe

dis: shader.exe
	mono shader.exe
	monodis ps.dll

all: shader.exe
