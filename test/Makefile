test-gen.exe: TestGen.cs Shaders.cs
	csc /out:test-gen.exe TestGen.cs Shaders.cs /r:PresentationCore.dll /r:WindowsBase.dll /r:PresentationFramework.dll

test-runner.exe: TestRunner.cs
	gmcs /out:test-runner.exe TestRunner.cs

generate-stamp: test-gen.exe
	mkdir -p references
	mkdir -p results
	testgen.exe
	touch generate-stamp

run-tests: test-runner.exe
	mkdir -p results
	mono test-runner.exe

all: test-gen.exe
