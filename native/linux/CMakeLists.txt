set(FFMPEG_VERSION "7.1")
set(FFMPEG_ARCHIVE_STEM "ffmpeg-n${FFMPEG_VERSION}-latest-linux64-lgpl-shared-${FFMPEG_VERSION}")
set(FFMPEG_URL "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/${FFMPEG_ARCHIVE_STEM}.tar.xz")
set(FFMPEG_EXTRACT_DIR "${CMAKE_BINARY_DIR}/ffmpeg")
set(FFMPEG_ROOT "${FFMPEG_EXTRACT_DIR}/${FFMPEG_ARCHIVE_STEM}")

if(NOT EXISTS "${FFMPEG_ROOT}/include/libavcodec/avcodec.h")
    message(STATUS "Downloading pre-built FFmpeg ${FFMPEG_VERSION}...")
    file(DOWNLOAD
        "${FFMPEG_URL}"
        "${CMAKE_BINARY_DIR}/ffmpeg.tar.xz"
        SHOW_PROGRESS
        STATUS download_status
    )
    list(GET download_status 0 status_code)
    if(NOT status_code EQUAL 0)
        message(FATAL_ERROR "Failed to download FFmpeg: ${download_status}")
    endif()

    file(MAKE_DIRECTORY "${FFMPEG_EXTRACT_DIR}")
    execute_process(
        COMMAND ${CMAKE_COMMAND} -E tar xf "${CMAKE_BINARY_DIR}/ffmpeg.tar.xz"
        WORKING_DIRECTORY "${FFMPEG_EXTRACT_DIR}"
        RESULT_VARIABLE extract_result
    )
    if(NOT extract_result EQUAL 0)
        message(FATAL_ERROR "Failed to extract FFmpeg archive")
    endif()
endif()

add_library(fennec_video SHARED
    fennec_encoder.c
    fennec_decoder.c
    fennec_capture_stub.c
)

target_include_directories(fennec_video PRIVATE
    ${CMAKE_SOURCE_DIR}
    "${FFMPEG_ROOT}/include"
)

target_link_directories(fennec_video PRIVATE
    "${FFMPEG_ROOT}/lib"
)

target_link_libraries(fennec_video PRIVATE
    avcodec
    avutil
    swscale
    swresample
)

set_target_properties(fennec_video PROPERTIES
    OUTPUT_NAME fennec_video
    INSTALL_RPATH "$ORIGIN"
    BUILD_WITH_INSTALL_RPATH TRUE
)

# Copy FFmpeg shared libs alongside libfennec_video.so after build
add_custom_command(TARGET fennec_video POST_BUILD
    COMMAND ${CMAKE_COMMAND}
        -DFFMPEG_LIB_DIR="${FFMPEG_ROOT}/lib"
        -DOUTPUT_DIR="$<TARGET_FILE_DIR:fennec_video>"
        -P "${CMAKE_CURRENT_SOURCE_DIR}/CopyFFmpegLibs.cmake"
    COMMENT "Copying FFmpeg shared libraries"
)
