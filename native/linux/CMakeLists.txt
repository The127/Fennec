find_package(PkgConfig REQUIRED)
pkg_check_modules(LIBAV REQUIRED libavcodec libavutil libswscale)

add_library(fennec_video SHARED
    fennec_encoder.c
    fennec_decoder.c
    fennec_capture_stub.c
)

target_include_directories(fennec_video PRIVATE
    ${CMAKE_SOURCE_DIR}
    ${LIBAV_INCLUDE_DIRS}
)

target_link_libraries(fennec_video PRIVATE
    ${LIBAV_LIBRARIES}
)

target_link_directories(fennec_video PRIVATE
    ${LIBAV_LIBRARY_DIRS}
)

set_target_properties(fennec_video PROPERTIES
    OUTPUT_NAME fennec_video
    INSTALL_RPATH "$ORIGIN"
)
