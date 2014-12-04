using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VehicleRegNo
{
    class Program
    {
        private static int[] GetExclusiveVehicleRegNos()
        {
            List exclusiveVehicleRegNosList = new List();

            // Range of all four digit numbers is 1000 to 9999
            int count = 0;
            for (int i = 1000; i < 9999; i++)
            {
                if (IsDigitsInAscendingOrder(i))
                {
                    if (FindRecursiveSumOfDigits(i) == 1)
                    {
                        exclusiveVehicleRegNosList.Add(i);
                        count++;
                    }

                }
            }

            return exclusiveVehicleRegNosList.ToArray();
        }

        private static int FindRecursiveSumOfDigits(int number)
        {
            // Find recursive sum till the sum become a single digit.
            // Eg:- 9999 => 9 + 9 + 9 + 9 = 36 => 3 + 6 = 9
            int sum = FindSumOfDigits(number);
            while (sum > 9)
            {
                sum = FindSumOfDigits(sum);
            }
            return sum;
        }

        private static int FindSumOfDigits(int number)
        {
            int sum = 0;
            while (number > 0)
            {
                sum += (number % 10);
                number /= 10;
            }
            return sum;
        }

        private static bool IsDigitsInAscendingOrder(int number)
        {
            bool isDigitsInAscendingOrder = false;

            // Getting the first digit
            int firstDigit = number / 1000;

            // Getting the second digit
            number %= 1000;
            int secondDigit = number / 100;

            // Getting the third digit
            number %= 100;
            int thirdDigit = number / 10;

            int fourthDigit = number % 10;

            if (firstDigit < secondDigit && secondDigit < thirdDigit 
                                            && thirdDigit < fourthDigit)
            {
                isDigitsInAscendingOrder = true;
            }

            return isDigitsInAscendingOrder;
        }

        static void Main(string[] args)
        {
            int[] exclusiveVehicleRegNos = GetExclusiveVehicleRegNos();
            string t = "";
            for (int i = 0; i < exclusiveVehicleRegNos.Length; i++)
            {
                // Printing those exclusive nos                
                Console.WriteLine(exclusiveVehicleRegNos[i]);
            }

            /********** Output *********************
            1234
            1279
            1369
            1378
            1459
            1468
            1567
            2359
            2368
            2458
            2467
            3457
            4789
            5689
            ****************************************/
        }

    }
}