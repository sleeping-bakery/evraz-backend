namespace CodeReview.Services;

public class StreamCloner
{
    public static Stream CloneStream(Stream original)
    {
        // Создаем новый поток памяти, в который будем копировать данные
        var memoryStream = new MemoryStream();
        
        // Копируем все данные из оригинального потока в новый поток
        original.Seek(0, SeekOrigin.Begin);  // Перемещаем указатель в начало потока
        original.CopyTo(memoryStream);

        // Сбрасываем позицию в новый поток в начало для последующего чтения
        memoryStream.Seek(0, SeekOrigin.Begin);
        
        return memoryStream;
    }
}