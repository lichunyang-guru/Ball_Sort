struct ProcessResult
{
    public readonly int ExitCode;
    public readonly string Output;

    public ProcessResult(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }
}