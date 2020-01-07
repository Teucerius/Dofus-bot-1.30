﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bot_Dofus_1._29._1.Comun.Network;
using Bot_Dofus_1._29._1.Utilities.Extensions;

/*
    Este archivo es parte del proyecto BotDofus_1.29.1
    BotDofus_1.29.1 Copyright (C) 2019 Alvaro Prendes — Todos los derechos reservados.
    Creado por Alvaro Prendes
    web: http://www.salesprendes.com
*/

namespace Bot_Dofus_1._29._1.Comun.Frames.Transporte
{
    public static class PaqueteRecibido
    {
        public static readonly List<PaqueteDatos> metodos = new List<PaqueteDatos>();

        public static void Inicializar()
        {
            Assembly asm = typeof(Frame).GetTypeInfo().Assembly;

            foreach (MethodInfo tipo in asm.GetTypes().SelectMany(x => x.GetMethods()).Where(m => m.GetCustomAttributes(typeof(PaqueteAtributo), false).Length > 0))
            {
                PaqueteAtributo atributo = tipo.GetCustomAttributes(typeof(PaqueteAtributo), true)[0] as PaqueteAtributo;
                Type tipo_string = Type.GetType(tipo.DeclaringType.FullName);

                object instancia = Activator.CreateInstance(tipo_string, null);
                metodos.Add(new PaqueteDatos(instancia, atributo.paquete, tipo));
            }
        }

        public static void Recibir(TcpClient client, string packet)
        {
            PaqueteDatos method = metodos.Find(m => packet.StartsWith(m.nombre_paquete));

            try
            {
                if (method != null)
                    method.information.Invoke(method.instance, new object[2] { client, packet });
            }
            catch(Exception ex)
            {
                client.account.Logger.LogException("Network", ex);
            }
        }
    }
}