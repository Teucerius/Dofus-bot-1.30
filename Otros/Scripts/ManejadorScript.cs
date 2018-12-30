﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bot_Dofus_1._29._1.Otros.Scripts.Acciones;
using Bot_Dofus_1._29._1.Otros.Scripts.Acciones.Mapas;
using Bot_Dofus_1._29._1.Otros.Scripts.Acciones.Peleas;
using Bot_Dofus_1._29._1.Otros.Scripts.Banderas;
using Bot_Dofus_1._29._1.Otros.Scripts.Manejadores;
using MoonSharp.Interpreter;

namespace Bot_Dofus_1._29._1.Otros.Scripts
{
    public class ManejadorScript : IDisposable
    {
        private Cuenta cuenta;
        private LuaManejadorScript manejador_script;
        private ManejadorAcciones manejar_acciones;
        private EstadoScript estado_script;
        private List<Bandera> banderas;
        private int bandera_id;
        private bool disposed;

        public bool activado { get; set; }
        public bool pausado { get; private set; }
        public bool corriendo => activado && !pausado;

        public event Action<string> evento_script_cargado;
        public event Action evento_script_iniciado;
        public event Action<string> evento_script_detenido;

        public ManejadorScript(Cuenta _cuenta)
        {
            cuenta = _cuenta;
            manejador_script = new LuaManejadorScript();
            manejar_acciones = new ManejadorAcciones(cuenta, manejador_script);
            banderas = new List<Bandera>();

            manejar_acciones.evento_accion_finalizada += get_Accion_Finalizada;
            cuenta.pelea.pelea_creada += get_Pelea_Creada;
            cuenta.pelea.pelea_acabada += get_Pelea_Acabada;
        }

        public void get_Desde_Archivo(string ruta_archivo)
        {
            if (activado)
                throw new Exception("Ya se está ejecutando un script.");

            if (!File.Exists(ruta_archivo) || !ruta_archivo.EndsWith(".lua"))
                throw new Exception("Archivo no encontrado o no es válido.");

            manejador_script.cargar_Desde_Archivo(ruta_archivo, despues_De_Archivo);
            evento_script_cargado?.Invoke(Path.GetFileNameWithoutExtension(ruta_archivo));
        }

        private void despues_De_Archivo()
        {
            manejador_script.Set_Global("imprimirExito", new Action<string>((mensaje) => cuenta.logger.log_informacion("Script", mensaje)));
            manejador_script.Set_Global("imprimirError", new Action<string>((mensaje) => cuenta.logger.log_Error("Script", mensaje)));
            manejador_script.Set_Global("detenerScript", new Action(() => detener_Script()));

            manejador_script.Set_Global("estaRecolectando", (Func<bool>)cuenta.esta_recolectando);
            manejador_script.Set_Global("estaDialogando", (Func<bool>)cuenta.esta_dialogando);
        }

        public void activar_Script()
        {
            if (manejador_script.script != null)
            {
                if (activado || cuenta.esta_ocupado)
                    return;

                activado = true;
                evento_script_iniciado?.Invoke();
                estado_script = EstadoScript.MOVIMIENTO;
                iniciar_Script();
            }
        }

        public void detener_Script(string mensaje = "Script pausado")
        {
            if (activado)
            {
                activado = false;
                pausado = false;
                banderas.Clear();
                bandera_id = 0;
                manejar_acciones.get_Borrar_Todo();
                evento_script_detenido?.Invoke(mensaje);
            }
        }

        private void iniciar_Script() => Task.Run(async () =>
        {
            if (!corriendo)
                return;
            try
            {
                await aplicar_Comprobaciones();

                if (!corriendo)
                    return;

                IEnumerable<Table> entradas = manejador_script.get_Entradas_Funciones(estado_script.ToString().ToLower());
                if (entradas == null)
                {
                    detener_Script($"La función {estado_script.ToString().ToLower()} no existe");
                    return;
                }

                foreach (Table entrada in entradas)
                {
                    if (entrada["mapa"] == null)
                        continue;

                    if (!cuenta.personaje.mapa.verificar_Mapa_Actual(int.Parse(entrada["mapa"].ToString())))
                        continue;

                    procesar_Entradas(entrada);
                    procesar_Actual_Entrada();
                    return;
                }

                detener_Script("Ninguna acción mas encontrada en el script");
            }
            catch (Exception ex)
            {
                cuenta.logger.log_Error("Script", ex.ToString());
            }
        });

        private async Task aplicar_Comprobaciones()
        {
            await verificar_Muerte();
        }

        private async Task verificar_Muerte()
        {
            if (cuenta.personaje.caracteristicas.energia_actual == 0)
            {
                cuenta.logger.log_informacion("SCRIPT", "El personaje esta muerto, pasando a modo fenix");
                estado_script = EstadoScript.FENIX;
            }
            await Task.Delay(50);
        }

        private void procesar_Entradas(Table entry)
        {
            banderas.Clear();
            bandera_id = 0;
            DynValue bandera = null;

            if (estado_script == EstadoScript.MOVIMIENTO)
            {
                bandera = entry.Get("pelea");
                if (!bandera.IsNil() && bandera.Type == DataType.Boolean && bandera.Boolean)
                {
                    banderas.Add(new PeleaBandera());
                }
            }

            bandera = entry.Get("celda");
            if (!bandera.IsNil() && bandera.Type == DataType.String)
            {
                banderas.Add(new CambiarMapa(bandera.String));
            }

            if (banderas.Count == 0)
            {
                detener_Script("No se ha encontrado ninguna acción en este mapa");
            }
        }

        private void procesar_Actual_Entrada(AccionesScript tiene_accion_disponible = null)
        {
            if (!corriendo)
                return;

            Bandera bandera_actual = banderas[bandera_id];

            if (bandera_actual is CambiarMapa mapa)
            {
                manejar_Cambio_Mapa(mapa);
            }
            else if (bandera_actual is PeleaBandera)
            {
                manejar_Pelea_mapa(tiene_accion_disponible as PeleasAccion);
            }
        }

        private void manejar_Cambio_Mapa(CambiarMapa mapa)
        {
            if (CambiarMapaAccion.TryParse(mapa.celda_id, out CambiarMapaAccion accion))
            {
                manejar_acciones.enqueue_Accion(accion, true);
            }
            else
            {
                detener_Script("La celda es invalida");
            }
        }

        private void manejar_Pelea_mapa(PeleasAccion pelea_accion)
        {
            PeleasAccion accion = pelea_accion ?? get_Crear_Pelea_Accion();
            
            if (cuenta.personaje.mapa.get_Puede_Luchar_Contra_Grupo_Monstruos(accion.monstruos_minimos, accion.monstruos_maximos, accion.monstruo_nivel_minimo, accion.monstruo_nivel_maximo, accion.monstruos_prohibidos, accion.monstruos_obligatorios))
            {
                manejar_acciones.enqueue_Accion(accion, true);
            }
            else
            {
                cuenta.logger.log_informacion("SCRIPT", "Ningún grupo de monstruos disponibles en este mapa");
                procesar_Actual_Bandera(true);
            }
        }

        private void procesar_Actual_Bandera(bool avoidChecks = false)
        {
            if (!corriendo)
                return;

            if (!avoidChecks)
            {
                if (banderas[bandera_id] is PeleaBandera)
                {
                    PeleasAccion accion_pelea = get_Crear_Pelea_Accion();

                    if (cuenta.personaje.mapa.get_Puede_Luchar_Contra_Grupo_Monstruos(accion_pelea.monstruos_minimos, accion_pelea.monstruos_maximos, accion_pelea.monstruo_nivel_minimo, accion_pelea.monstruo_nivel_maximo, accion_pelea.monstruos_prohibidos, accion_pelea.monstruos_obligatorios))
                    {
                        procesar_Actual_Entrada(accion_pelea);
                        return;
                    }
                }
            }

            bandera_id++;
            if (bandera_id == banderas.Count)
            {
                detener_Script("No se ha encontrado ninguna acción en este mapa");
            }
            else
            {
                procesar_Actual_Entrada();
            }
        }

        private PeleasAccion get_Crear_Pelea_Accion()
        {
            int monstruos_minimos = manejador_script.get_Global_Or("MONSTRUOS_MINIMOS", DataType.Number, 1);
            int monstruos_maximos = manejador_script.get_Global_Or("MONSTRUOS_MAXIMOS", DataType.Number, 8);
            int monstruo_nivel_minimo = manejador_script.get_Global_Or("MINIMO_NIVEL_MONSTRUOS", DataType.Number, 1);
            int monstruo_nivel_maximo = manejador_script.get_Global_Or("MAXIMO_NIVEL_MONSTRUOS", DataType.Number, 1000);
            List<int> monstruos_prohibidos = new List<int>();
            List<int> monstruos_obligatorios = new List<int>();

            Table entrada = manejador_script.get_Global_Or<Table>("MONSTRUOS_PROHIBIDOS", DataType.Table, null);
            if (entrada != null)
            {
                foreach (var fm in entrada.Values)
                {
                    if (fm.Type != DataType.Number)
                        continue;

                    monstruos_prohibidos.Add((int)fm.Number);
                }
            }
            entrada = manejador_script.get_Global_Or<Table>("MONSTRUOS_OBLIGATORIOS", DataType.Table, null);
            if (entrada != null)
            {
                foreach (var mm in entrada.Values)
                {
                    if (mm.Type != DataType.Number)
                        continue;
                    monstruos_obligatorios.Add((int)mm.Number);
                }
            }
            return new PeleasAccion(monstruos_minimos, monstruos_maximos, monstruo_nivel_minimo, monstruo_nivel_maximo, monstruos_prohibidos, monstruos_obligatorios);
        }

        #region Zona Eventos
        private void get_Accion_Finalizada(bool mapa_cambiado)
        {
            if (mapa_cambiado)
            {
                iniciar_Script();
            }
            else
            {
                procesar_Actual_Bandera();
            }
        }

        private void get_Pelea_Creada()
        {
            if (activado)
                pausado = true;
        }

        private void get_Pelea_Acabada()
        {
            if (activado)
                pausado = false;
        }
        #endregion

        #region Zona Dispose
        ~ManejadorScript() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                if (disposing)
                {
                    manejador_script.Dispose();
                    manejar_acciones.Dispose();
                }
                manejar_acciones = null;
                manejador_script = null;
                activado = false;
                cuenta = null;
                disposed = true;
            }
        }
        #endregion
    }
}
