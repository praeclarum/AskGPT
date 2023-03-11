namespace AskGPT;

class Formatter
{
    public void Format(string markdown)
    {
        Output(markdown);
    }

    void Output(string text)
    {
        Console.Write(text);
    }    
}
