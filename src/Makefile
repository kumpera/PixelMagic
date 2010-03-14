FILES = Cairo.cs \
	Compiler.cs	\
	Driver.cs	\
	Instruction.cs	\
	Interpreter.cs	\
	Options.cs	\
	Parser.cs	\
	Register.cs	\
	ShaderData.cs	\
	SimdExtras.cs	\
	Texture.cs	\
	Tracing.cs
	
shader.exe: ${FILES}
	gmcs /unsafe -debug /r:Mono.Simd /out:shader.exe ${FILES}

run: shader.exe
	mono shader.exe

dis: shader.exe
	mono shader.exe
	monodis ps.dll

all: shader.exe
