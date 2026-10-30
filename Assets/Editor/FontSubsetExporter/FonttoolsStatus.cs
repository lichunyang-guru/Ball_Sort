struct FonttoolsStatus
{
    public readonly bool IsAvailable;
    public readonly string PythonExecutable;
    public readonly string Version;
    public readonly string Output;

    public FonttoolsStatus(bool isAvailable, string pythonExecutable, string version, string output)
    {
        IsAvailable = isAvailable;
        PythonExecutable = pythonExecutable;
        Version = version;
        Output = output;
    }
}