
## publishing
publish:
	npm publish --access public

## formatting makerules
ALL_SOURCE_FILES := \
	$(shell fd ".*\.h"   -- Editor)  \
	$(shell fd ".*\.c"   -- Editor)  \
	$(shell fd ".*\.cpp" -- Editor)

ALL_TRACKED_FILES := \
	$(shell git ls-files -- Editor | rg ".*\.h")    \
	$(shell git ls-files -- Editor | rg ".*\.c")    \
	$(shell git ls-files -- Editor | rg ".*\.cpp")

ALL_MODIFIED_FILES := \
	$(shell git lsm -- Editor)


format-all: $(ALL_SOURCE_FILES)
	clang-format -i $^

format: $(ALL_TRACKED_FILES)
	clang-format -i $^
	#$(GENIE) format

q qformat: $(ALL_MODIFIED_FILES)
	clang-format -i $^

