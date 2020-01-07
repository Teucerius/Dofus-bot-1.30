﻿using System.Reflection;

/*
    Este archivo es parte del proyecto BotDofus_1.29.1
    BotDofus_1.29.1 Copyright (C) 2019 Alvaro Prendes — Todos los derechos reservados.
    Creado por Alvaro Prendes
    web: http://www.salesprendes.com
*/

namespace Bot_Dofus_1._29._1.Comun.Frames.Transporte
{
    public class PaqueteDatos
    {
        public object instance { get; set; }
        public string nombre_paquete { get; set; }
        public MethodInfo information { get; set; }

        public PaqueteDatos(object _instancia, string _nombre_paquete, MethodInfo _informacion)
        {
            instance = _instancia;
            nombre_paquete = _nombre_paquete;
            information = _informacion;
        }
    }
}