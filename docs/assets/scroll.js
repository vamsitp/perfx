// utilities
var getAll = function (selector, scope) {
    scope = scope ? scope : document;
    return scope.querySelectorAll(selector);
};

//in page scrolling for documentaiton page
var btns = getAll('.js-btn');
var sections = getAll('.js-section');
if (btns.length && sections.length > 0) {
    // for (var i = 0; i<btns.length; i++) {
    //   btns[i].addEventListener('click', function(event) {
    //     smoothScrollTo(sections[i], event);
    //   });
    // }
    btns[0].addEventListener('click', function (event) {
        smoothScrollTo(sections[0], event);
    });

    btns[1].addEventListener('click', function (event) {
        smoothScrollTo(sections[1], event);
    });

    btns[2].addEventListener('click', function (event) {
        smoothScrollTo(sections[2], event);
    });

    btns[3].addEventListener('click', function (event) {
        smoothScrollTo(sections[3], event);
    });

    btns[4].addEventListener('click', function (event) {
        smoothScrollTo(sections[4], event);
    });

    btns[5].addEventListener('click', function (event) {
        smoothScrollTo(sections[5], event);
    });

    btns[6].addEventListener('click', function (event) {
        smoothScrollTo(sections[6], event);
    });
}