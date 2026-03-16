add_library(fennec_video SHARED
    fennec_capture.c
    fennec_encoder.c
    fennec_decoder.c
)

target_include_directories(fennec_video PRIVATE ${CMAKE_SOURCE_DIR})
target_compile_definitions(fennec_video PRIVATE FENNEC_VIDEO_EXPORTS)

target_link_libraries(fennec_video PRIVATE
    d3d11
    dxgi
    mfplat
    mfreadwrite
    mf
    mfuuid
    ole32
    shlwapi
)

set_target_properties(fennec_video PROPERTIES
    OUTPUT_NAME fennec_video
)
