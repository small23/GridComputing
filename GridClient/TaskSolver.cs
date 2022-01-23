using System;
using System.CodeDom;
using System.Security.Cryptography;
using System.Text;

namespace GridClient
{
    internal class TaskSolver
    {
        public string TaskData = "";
        public string Result = "";
        public double Prorgess = 0;
        public bool IsDone = false;
        public bool Interrupt = false;
        public void Run()
        {
            var base64EncodedBytes = System.Convert.FromBase64String(TaskData);
            var text = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            var Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" + "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToLower() + "1234567890,.;-";
            int[] arrayData= new int[Letters.Length];
            Array.Clear(arrayData, 0, arrayData.Length);
            // Итеративный цикл
            for (int i = 0; i < text.Length; i++)
            {
                //Подсчет числа букв в массиве
                for (int j = 0; j < Letters.Length; j++)
                {
                    if (text[i] == Letters[j])
                    {
                        arrayData[j]++;
                        break;
                    }
                }
                // Обновление прогресс-бара
                double prorgess2 = (double)i / (double)(Letters.Length);
                if (prorgess2 > 1)
                    prorgess2 = 1;
                Prorgess = prorgess2;
                // Контроль флага, в случае прерывания - выход
                if (Interrupt)
                    return;
            }
            // Результаты в строку
            String Result = "";
            for (int j = 0; j < Letters.Length; j++)
            {
                Result += arrayData[j].ToString() + ";";
            }
            // Конвертация результата в Base64
            Result = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Result));
            // Сигнализируем о том, что поток закончил работу
            IsDone = true;
        }
    }
}
