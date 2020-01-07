﻿using Bot_Dofus_1._29._1.Otros.Mapas.Entidades;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
    Este archivo es parte del proyecto BotDofus_1.29.1

    BotDofus_1.29.1 Copyright (C) 2019 Alvaro Prendes — Todos los derechos reservados.
    Creado por Alvaro Prendes
    web: http://www.salesprendes.com
*/

namespace Bot_Dofus_1._29._1.Otros.Scripts.Acciones.Npcs
{
    public class NpcAction : ScriptAction
    {
        public int npc_id { get; private set; }

        public NpcAction(int _npc_id)
        {
            npc_id = _npc_id;
        }

        internal override Task<ResultadosAcciones> process(Account cuenta)
        {
            if (cuenta.Is_Busy())
                return resultado_fallado;

            Otros.Mapas.Entidades.Npcs npc = null;
            IEnumerable<Otros.Mapas.Entidades.Npcs> npcs = cuenta.game.map.lista_npcs();

            if (npc_id < 0)
            {
                int index = (npc_id * -1) - 1;

                if (npcs.Count() <= index)
                    return resultado_fallado;

                npc = npcs.ElementAt(index);
            }
            else
                npc = npcs.FirstOrDefault(n => n.npc_modelo_id == npc_id);

            if (npc == null)
                return resultado_fallado;

            cuenta.connexion.SendPacket("DC" + npc.id, true);
            return resultado_procesado;
        }
    }
}
