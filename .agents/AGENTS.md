# Guía de Estilos y Estructura de Formularios - Manita De Gato

Esta regla de proyecto define el estándar visual y de accesibilidad establecido para todos los formularios de la plataforma (Registro, Login, Creación y Edición de Entidades). Debe aplicarse de forma constante en cualquier desarrollo de vistas en el proyecto.

## 1. Contenedor del Formulario (Estructura y Centrado)
Para garantizar un centrado vertical y horizontal perfecto sin necesidad de scrolling innecesario en la pantalla del usuario, la estructura del contenedor debe ser la siguiente:

```html
<div class="min-h-[85vh] flex items-center justify-center py-6 px-4 sm:px-6 lg:px-8">
    <div class="max-w-xl w-full space-y-6 neo-card p-8 sm:p-10 rounded-2xl shadow-xl border border-pink-500/20 relative overflow-hidden">
        <!-- Glows de fondo decorativos -->
        <div class="absolute top-0 right-0 -mr-8 -mt-8 w-32 h-32 rounded-full bg-pink-500/10 opacity-50"></div>
        <div class="absolute bottom-0 left-0 -ml-8 -mb-8 w-24 h-24 rounded-full bg-pink-500/20 opacity-50"></div>
        
        <div class="relative z-10">
            <!-- Contenido del Formulario -->
        </div>
    </div>
</div>
```

---

## 2. Tipografía y Encabezado del Formulario
* **Icono Animado**: De tamaño `text-5xl` con animación `animate-pulse` y color `text-pink-500`.
* **Título**: Tamaño `text-3xl sm:text-4xl font-black text-white tracking-tight`.
* **Subtítulo/Descripción**: Tamaño `text-sm text-gray-400 mt-1`.

---

## 3. Elementos de Formulario (Inputs y Labels)
Para asegurar la legibilidad, accesibilidad táctil/clicable y concordancia de colores claros y oscuros, se aplican las siguientes especificaciones:

* **Etiquetas (Labels)**: 
  * Clase: `block text-sm font-semibold text-gray-400 mb-1.5`
* **Cajas de Entrada (Inputs)**:
  * Clase: `w-full pl-10 pr-4 py-3 border border-gray-300 dark:border-gray-700/50 rounded-lg bg-white/50 dark:bg-gray-800/30 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-600 focus:ring-2 focus:ring-pink-500 focus:border-transparent transition-all duration-200 text-base`
  * *Nota*: Para los iconos internos a la izquierda del input, colocarlos en un div absolute con padding `pl-3.5` e icono de tamaño `text-sm`.
* **Placeholders**: Deben ser lo suficientemente suaves (`placeholder-gray-400` en claro y `dark:placeholder-gray-600` en oscuro) para que no parezca que el campo ya tiene texto digitado.

---

## 4. Separadores de Sección Rosados
Para agrupar temáticamente los campos dentro de un formulario (como separar datos personales de datos de acceso), usar el siguiente diseño:

```html
<div class="relative py-4">
    <div class="absolute inset-0 flex items-center" aria-hidden="true">
        <div class="w-full border-t border-pink-500/30"></div>
    </div>
    <div class="relative flex justify-center text-xs uppercase font-bold tracking-widest">
        <span class="px-4 text-pink-500 dark:text-pink-400 rounded border border-pink-500/20 py-1 shadow-sm transition-colors duration-500" style="background-color: var(--bg-color)">
            [Título de la Sección]
        </span>
    </div>
</div>
```
* **Importante**: El fondo del texto (`span`) debe definirse utilizando la variable CSS de tema `style="background-color: var(--bg-color)"` para ocultar la línea divisoria sin importar si la página está en modo claro u oscuro.

---

## 5. Botones de Acción
* **Botón Principal**:
  * Clase: `w-full flex justify-center py-3.5 px-4 border border-transparent text-base font-bold rounded-lg text-real-white bg-pink-600 hover:bg-pink-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-pink-500 transition-all shadow-md hover:shadow-lg`
* **Botones Secundarios/Cancelar**:
  * Clase: `px-6 py-3 border border-gray-300 dark:border-gray-700 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-500/10 font-semibold text-base`
