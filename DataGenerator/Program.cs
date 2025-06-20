public class Program
{
    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Запуск системы генерации данных университета");

        try
        {
            var totalGenerator = new TotalGenerator();
            await totalGenerator.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Необработанное исключение: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Завершение работы");
        }
    }
}