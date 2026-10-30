struct PythonExecutable
{
    public readonly string FileName;
    public readonly string[] Arguments;

    public PythonExecutable(string fileName, string[] arguments)
    {
        FileName = fileName;
        Arguments = arguments;
    }
}