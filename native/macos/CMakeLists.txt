add_library(fennec_video SHARED
    fennec_capture.m
    fennec_encoder.m
    fennec_decoder.m
)

target_include_directories(fennec_video PRIVATE ${CMAKE_SOURCE_DIR})

find_library(SCREENCAPTUREKIT ScreenCaptureKit)
find_library(VIDEOTOOLBOX VideoToolbox)
find_library(COREMEDIA CoreMedia)
find_library(COREVIDEO CoreVideo)
find_library(COREGRAPHICS CoreGraphics)
find_library(FOUNDATION Foundation)
find_library(APPKIT AppKit)

target_link_libraries(fennec_video PRIVATE
    ${SCREENCAPTUREKIT}
    ${VIDEOTOOLBOX}
    ${COREMEDIA}
    ${COREVIDEO}
    ${COREGRAPHICS}
    ${FOUNDATION}
    ${APPKIT}
)

set_target_properties(fennec_video PROPERTIES
    OUTPUT_NAME fennec_video
    INSTALL_RPATH "@loader_path"
)
