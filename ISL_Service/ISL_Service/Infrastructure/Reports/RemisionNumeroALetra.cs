namespace ISL_Service.Infrastructure.Reports;

/*
  EL IMPORTE CON LETRA DE LA REMISION

  Copia literal de Utils/ConvertirNumeroALetra.cs de legacy (MacServicios2),
  con sus rarezas de ortografia incluidas. No se corrige ninguna: lo que tiene
  que salir en el papel es exactamente lo que hoy imprime Mac31.

  Si algun dia se decide corregirlo, tiene que corregirse en los dos lados el
  mismo dia, o la misma remision dira una cosa en el web y otra en Mac31.
*/
public static class RemisionNumeroALetra
{

        public static string ConvertirNumero(string sNumero)
        {
            string ResultadoFinal = "";
            string resultado_cent = "";
            string resultado = "";
            string NumeroDer;
            string NumeroIzq;
            int pesos = 0;
            int miles = 0;
            int millones = 0;

            sNumero = sNumero.Replace(",", "");

            string[] p = sNumero.Split('.');
            NumeroDer = "";

            NumeroIzq = p[0];

            if (NumeroDer.Length == 0)
            {
                resultado_cent = " 00/100 ";
            }

            if (p.Length > 1)
            {
                NumeroDer = p[1];

                if (NumeroDer.Length <= 2)
                {
                    decimal.Parse(NumeroDer);
                    resultado_cent = NumeroDer + "/100 ";
                }

            }

            if (NumeroIzq.Length > 3)
            {
                int res = NumeroIzq.Length - 3;
                pesos = int.Parse(NumeroIzq.Substring(res, 3));

            }
            else
            {
                pesos = int.Parse(NumeroIzq);
            }


            if (int.Parse(NumeroIzq) == 1)
            {

                resultado = "UN PESO ";
                ResultadoFinal = resultado.ToLower() + resultado_cent.ToLower() + " M. N.";
            }

            if (int.Parse(NumeroIzq) != 1)
            {
                resultado = Proceso(pesos);
                resultado = resultado + " PESOS ";
                ResultadoFinal = resultado.ToLower() + resultado_cent.ToLower() + " M. N.";
            }

            string resultadouno = resultado;

            string resultadodos = "";

            if (NumeroIzq.Length > 3 && NumeroIzq.Length <= 9)
            {

                if (NumeroIzq.Length >= 6 && NumeroIzq.Length <= 9)
                {
                    int re = NumeroIzq.Length - 6;
                    miles = int.Parse(NumeroIzq.Substring(re, 3));
                }
                if (NumeroIzq.Length > 3 && NumeroIzq.Length <= 6)
                {
                    int re = NumeroIzq.Length - 3;
                    miles = int.Parse(NumeroIzq.Substring(0, re));
                }
                resultado = "";
                resultado = Proceso(miles);
                if (pesos == 000)
                {
                    resultadouno = " PESOS ";
                }
                resultado = resultado + " MIL " + resultadouno;
                ResultadoFinal = resultado.ToLower() + resultado_cent.ToLower() + " M. N.";
                resultadodos = resultado;
            }


            if (NumeroIzq.Length > 6 && NumeroIzq.Length <= 9)
            {
                int res = NumeroIzq.Length - 6;
                millones = int.Parse(NumeroIzq.Substring(0, res));
                resultado = "";
                resultado = Proceso(millones);
                if (millones == 1 && miles == 000 && pesos == 000)
                {
                    resultado = " UN MILLON DE PESOS ";
                }
                if (millones >= 2 && miles == 000 && pesos == 000)
                {
                    resultado = resultado + " MILLONES DE PESOS ";
                }
                if (millones != 0 && miles != 0)
                {
                    resultado = resultado + " MILLONES " + resultadodos;
                }
                ResultadoFinal = resultado.ToLower() + resultado_cent.ToLower() + " M. N.";

            }

            return ResultadoFinal.ToUpper();
        }
        

        
        private static string Proceso(int NumeroConventir)
        {
            string resultado = "";
            int Unidades = -1;
            int Decimas = -1;
            int Centesimas = -1;
            int Milesimas = -1;
            int DiezMilesimas = -1;
            int CienMilesimas = -1;
            int Millon = -1;

            for (int j = 0; j < NumeroConventir.ToString().Length; j++)
            {

                int numerodeespacios = NumeroConventir.ToString().Length - 1 - j;

                int valorJnumer = int.Parse(NumeroConventir.ToString().Substring(numerodeespacios, 1));


                if (j == 0)
                {
                    Unidades = valorJnumer;

                }
                if (j == 1)
                {
                    Decimas = valorJnumer;
                }
                if (j == 2)
                {
                    Centesimas = valorJnumer;
                }
                if (j == 3)
                {
                    Milesimas = valorJnumer;
                }
                if (j == 4)
                {
                    DiezMilesimas = valorJnumer;
                }
                if (j == 5)
                {
                    CienMilesimas = valorJnumer;
                }
                if (j == 6)
                {
                    Millon = valorJnumer;
                }

                bool Procesar = true;


                if (j == 1)
                {
                    if (Decimas == 1)
                    {
                        resultado = "DIECI" + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 2)
                    {
                        resultado = "VEINTI" + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 3)
                    {
                        resultado = "TREINTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 4)
                    {
                        resultado = "CUARENTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 5)
                    {
                        resultado = "CINCUENTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 6)
                    {
                        resultado = "SESENTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 7)
                    {
                        resultado = "SETENTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 8)
                    {
                        resultado = "OCHENTA Y " + resultado;
                        Procesar = false;
                    }
                    if (Decimas == 9)
                    {
                        resultado = "NOVENTA Y " + resultado;
                        Procesar = false;
                    }
                }

                if (Centesimas == 1)
                {
                    resultado = "CIENTO " + resultado;
                    Procesar = false;
                }
                if (Centesimas == 2)
                {

                    if (Centesimas == 2 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIENTOS";
                    }
                    else
                    {
                        resultado = "CIENTOS " + resultado;
                    }
                    Procesar = true;
                }
                if (Centesimas == 3)
                {

                    if (Centesimas == 3 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIENTOS";
                    }
                    else
                    {
                        resultado = "CIENTOS " + resultado;
                    }
                    Procesar = true;
                }
                if (Centesimas == 4)
                {

                    if (Centesimas == 4 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIENTOS";
                    }
                    else
                    {
                        resultado = "CIENTOS " + resultado;
                    }
                    Procesar = true;
                }

                if (Centesimas == 5)
                {

                    if (Centesimas == 5 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "";
                        resultado = "QUINIENTOS";
                    }
                    else
                    {
                        resultado = "QUINIENTOS " + resultado;
                    }
                    Procesar = false;
                }
                if (Centesimas == 6)
                {

                    if (Centesimas == 6 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIENTOS";
                    }
                    else
                    {
                        resultado = "CIENTOS " + resultado;
                    }
                    Procesar = true;
                }
                if (Centesimas == 7)
                {

                    if (Centesimas == 7 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "";
                        resultado = "SETECIENTOS";
                    }
                    else
                    {
                        resultado = "SETECIENTOS " + resultado;
                    }
                    Procesar = false;
                }
                if (Centesimas == 8)
                {

                    if (Centesimas == 8 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIENTOS";
                    }

                    else
                    {
                        resultado = "CIENTOS " + resultado;

                    }
                    Procesar = true;
                }
                if (Centesimas == 9)
                {

                    if (Centesimas == 9 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "";
                        resultado = "NOVECIENTOS";
                    }

                    else
                    {
                        resultado = "NOVECIENTOS " + resultado;
                    }
                    Procesar = false;
                }


                if (Procesar == true)
                {
                    if (valorJnumer == 0)
                    {
                        if (Decimas != 0 && Centesimas != 0)
                        {
                            resultado = "CERO";
                        }
                    }
                    else if (valorJnumer == 1)
                    {
                        resultado = "UN";
                    }
                    else if (valorJnumer == 2)
                    {
                        resultado = "DOS" + resultado;
                    }
                    else if (valorJnumer == 3)
                    {
                        resultado = "TRES" + resultado;
                    }
                    else if (valorJnumer == 4)
                    {
                        resultado = "CUATRO" + resultado;
                    }
                    else if (valorJnumer == 5)
                    {
                        resultado = "CINCO" + resultado;
                    }
                    else if (valorJnumer == 6)
                    {
                        resultado = "SEIS" + resultado;
                    }
                    else if (valorJnumer == 7)
                    {
                        resultado = "SIETE" + resultado;
                    }
                    else if (valorJnumer == 8)
                    {
                        resultado = "OCHO" + resultado;
                    }
                    else if (valorJnumer == 9)
                    {
                        resultado = "NUEVE" + resultado;
                    }
                }

                if (j == 1)
                {
                    // Excepciones
                    if (Decimas == 1 && Unidades == 0)
                    {
                        resultado = "DIEZ";
                    }
                    if (Decimas == 1 && Unidades == 1)
                    {
                        resultado = "ONCE";
                    }
                    else if (Decimas == 1 && Unidades == 2)
                    {
                        resultado = "DOCE";
                    }
                    else if (Decimas == 1 && Unidades == 3)
                    {
                        resultado = "TRECE";
                    }
                    else if (Decimas == 1 && Unidades == 4)
                    {
                        resultado = "CATORCE";
                    }
                    else if (Decimas == 1 && Unidades == 5)
                    {
                        resultado = "QUINCE";
                    }
                    else if (Decimas == 2 && Unidades == 0)
                    {
                        resultado = "VEINTE";
                    }
                    else if (Decimas == 3 && Unidades == 0)
                    {
                        resultado = "TREINTA";
                    }
                    else if (Decimas == 4 && Unidades == 0)
                    {
                        resultado = "CUARENTA";
                    }
                    else if (Decimas == 5 && Unidades == 0)
                    {
                        resultado = "CINCUENTA";
                    }
                    else if (Decimas == 6 && Unidades == 0)
                    {
                        resultado = "SESENTA";
                    }
                    else if (Decimas == 7 && Unidades == 0)
                    {
                        resultado = "SETENTA";
                    }
                    else if (Decimas == 8 && Unidades == 0)
                    {
                        resultado = "OCHENTA";
                    }
                    else if (Decimas == 9 && Unidades == 0)
                    {
                        resultado = "NOVENTA";
                    }
                }

                if (j == 2)
                {
                    if (Centesimas == 1 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "CIEN";
                    }
                    if (Centesimas == 5 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "QUINIENTOS";
                    }
                    if (Centesimas == 7 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "SETECIENTOS";
                    }
                    if (Centesimas == 9 && Decimas == 0 && Unidades == 0)
                    {
                        resultado = "NOVECIENTOS";
                    }
                }

            }

            return resultado;
        }
        
}
