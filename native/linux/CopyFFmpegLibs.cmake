file(GLOB ffmpeg_libs
    "${FFMPEG_LIB_DIR}/libavcodec.so.*"
    "${FFMPEG_LIB_DIR}/libavutil.so.*"
    "${FFMPEG_LIB_DIR}/libswscale.so.*"
)

foreach(lib ${ffmpeg_libs})
    if(NOT IS_SYMLINK "${lib}")
        file(COPY "${lib}" DESTINATION "${OUTPUT_DIR}")
    endif()
endforeach()
