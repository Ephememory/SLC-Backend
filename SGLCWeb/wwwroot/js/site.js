// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

window.onload = function () {
    let testButton = document.getElementById("test-button");
    testButton.addEventListener("click", (event) => {
        alert("you clicked it!");
    });
}