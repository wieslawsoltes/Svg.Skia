// App-owned WebGL2 context, using only Emscripten's public HTML5/GL APIs.
// Skia's GL calls stay native; no Uno GLCanvasElement or private JS context table
// is involved. See https://emscripten.org/docs/api_reference/html5.h.html.
#include <GLES3/gl3.h>
#include <emscripten.h>
#include <emscripten/html5.h>
#include <emscripten/html5_webgl.h>
#include <stdint.h>
#include <string.h>

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_create(const char* selector)
{
    EmscriptenWebGLContextAttributes attributes;
    emscripten_webgl_init_context_attributes(&attributes);
    attributes.alpha = EM_TRUE;
    attributes.depth = EM_FALSE;
    attributes.stencil = EM_TRUE;
    attributes.antialias = EM_FALSE; // Skia supplies its normal coverage AA.
    attributes.premultipliedAlpha = EM_TRUE;
    attributes.preserveDrawingBuffer = EM_FALSE; // Copy occurs in the drawing task.
    attributes.majorVersion = 2;
    attributes.minorVersion = 0;
    attributes.enableExtensionsByDefault = EM_TRUE;
    attributes.explicitSwapControl = EM_FALSE;
    attributes.renderViaOffscreenBackBuffer = EM_FALSE;
    return emscripten_webgl_create_context(selector, &attributes);
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_current(void)
{
    return emscripten_webgl_get_current_context();
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_make_current(int context)
{
    return emscripten_webgl_make_context_current(context);
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_is_lost(int context)
{
    return emscripten_is_webgl_context_lost(context);
}

EMSCRIPTEN_KEEPALIVE
void uno_draw_gl_destroy(int context)
{
    emscripten_webgl_destroy_context(context);
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_resize(const char* selector, int width, int height)
{
    return emscripten_set_canvas_element_size(selector, width, height);
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_buffer_size(int context, int* width, int* height)
{
    return emscripten_webgl_get_drawing_buffer_size(context, width, height);
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_framebuffer(int* stencil, int* samples, int* maximum_width, int* maximum_height)
{
    GLint viewport[2];
    GLint renderbuffer;
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    glGetIntegerv(GL_STENCIL_BITS, stencil);
    glGetIntegerv(GL_SAMPLES, samples);
    glGetIntegerv(GL_MAX_VIEWPORT_DIMS, viewport);
    glGetIntegerv(GL_MAX_RENDERBUFFER_SIZE, &renderbuffer);
    *maximum_width = viewport[0] < renderbuffer ? viewport[0] : renderbuffer;
    *maximum_height = viewport[1] < renderbuffer ? viewport[1] : renderbuffer;
    return glGetError();
}

EMSCRIPTEN_KEEPALIVE
int uno_draw_gl_error(void)
{
    return glGetError();
}

// WebGL2 renderable allocations require sized formats. This is the same small
// compatibility adjustment used by Uno 7.0.32's public native GL resolver:
// https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Runtime.WebAssembly.Browser/build/native/uno_gl_shim.c
static GLint sized_format(GLint format, GLenum type)
{
    if (type == GL_UNSIGNED_BYTE)
    {
        if (format == GL_RGB) return GL_RGB8;
        if (format == GL_RGBA) return GL_RGBA8;
    }
    return format;
}

static void texture_image_2d(GLenum target, GLint level, GLint internal_format,
    GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, const void* pixels)
{
    glTexImage2D(target, level, sized_format(internal_format, type), width, height, border, format, type, pixels);
}

static void texture_image_3d(GLenum target, GLint level, GLint internal_format,
    GLsizei width, GLsizei height, GLsizei depth, GLint border, GLenum format, GLenum type, const void* pixels)
{
    glTexImage3D(target, level, sized_format(internal_format, type), width, height, depth, border, format, type, pixels);
}

EMSCRIPTEN_KEEPALIVE
void* uno_draw_gl_resolve(const char* name)
{
    if (strcmp(name, "glTexImage2D") == 0) return (void*)(intptr_t)&texture_image_2d;
    if (strcmp(name, "glTexImage3D") == 0) return (void*)(intptr_t)&texture_image_3d;
    return emscripten_webgl_get_proc_address(name);
}
